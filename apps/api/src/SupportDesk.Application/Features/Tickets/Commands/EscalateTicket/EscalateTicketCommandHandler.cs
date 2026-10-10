using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>Applies one-level escalation and saves a before/after audit row atomically.</summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    SlaDeadlineCalculator slaCalculator,
    AutomaticAgentSelector selector,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    public async Task<EscalateTicketResponse> HandleAsync(
        int ticketId,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        // Normalize first: even malformed JSON/null values cannot bypass validation.
        var reason = request.Reason?.Trim() ?? string.Empty;
        var escalatedBy = request.EscalatedBy?.Trim() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();
        if (reason.Length is < 5 or > 500)
            errors[nameof(request.Reason)] = ["Reason must contain 5 to 500 characters."];
        if (escalatedBy.Length is < 1 or > 100)
            errors[nameof(request.EscalatedBy)] = ["EscalatedBy is required (maximum 100 characters)."];
        if (errors.Count > 0)
            throw new ValidationException(errors);

        var ticket = await tickets.GetByIdAsync(ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);

        // Reject before loading related data, with the specific conflict reason.
        if (!ticket.IsOpen)
            throw new BusinessRuleViolationException(
                $"Ticket {ticket.Reference} is {ticket.Status} and cannot be escalated.");

        if (ticket.Priority == TicketPriority.Critical)
            throw new BusinessRuleViolationException(
                $"Ticket {ticket.Reference} is already Critical and cannot be escalated.");

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;
        var nextPriority = (TicketPriority)((int)ticket.Priority + 1);
        var newDueAt = slaCalculator.CalculateDueAtUtc(nextPriority, customer.Tier, now);

        var candidates = await agents.GetAssignmentCandidatesAsync(ticket.CategoryId, ct);
        int? newAgentId;

        // Retaining the existing agent is not a new assignment.
        // Thus they may retain it even when workload == capacity.
        var current = ticket.AssignedAgentId is int currentId
            ? candidates.FirstOrDefault(agent => agent.AgentId == currentId)
            : null;

        var canRetain = current is not null
            && current.IsActive
            && (!category.RequiresSpecialist || current.IsSpecialist)
            && current.OpenTicketCount <= current.MaxOpenTickets;

        if (canRetain)
        {
            newAgentId = ticket.AssignedAgentId;
        }
        else
        {
            newAgentId = selector.Select(candidates, category.RequiresSpecialist).AgentId;
        }

        var history = ticket.Escalate(
            reason,
            escalatedBy,
            now,
            newDueAt,
            newAgentId);

        // One SaveChanges transaction includes the ticket and its escalation audit row.
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {From} to {To} by {Actor}.",
            ticket.Reference, history.FromPriority, history.ToPriority, history.EscalatedBy);

        return new EscalateTicketResponse(
            await getTicket.HandleAsync(ticketId, ct),
            TicketEscalationDto.From(history));
    }
}
