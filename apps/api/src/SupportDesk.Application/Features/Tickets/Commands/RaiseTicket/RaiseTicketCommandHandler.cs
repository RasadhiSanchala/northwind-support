
using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Creates tickets with automatic priority,
/// configurable SLA, agent assignment and
/// human-readable triage explanations.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    SlaDeadlineCalculator slaCalculator,
    AutomaticAgentSelector agentSelector,
    ILogger<RaiseTicketCommandHandler> logger)
{
    public async Task<TicketDetailDto> HandleAsync(
        CreateTicketRequest request,
        CancellationToken ct)
    {
        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException(
                "Customer",
                request.CustomerId);
        }

        var customer = await customers.GetByIdAsync(
            request.CustomerId,
            ct);

        if (customer is null)
        {
            throw new NotFoundException(
                "Customer",
                request.CustomerId);
        }

        var category = await categories.GetByIdAsync(
            request.CategoryId,
            ct);

        if (category is null)
        {
            throw new NotFoundException(
                "Category",
                request.CategoryId);
        }

        // Priority classification.
        var priority = category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : request.RequestedPriority ?? TicketPriority.Medium;

        var priorityReason = category.ForcesCriticalPriority
            ? $"Category '{category.Name}' forces Critical priority."
            : request.RequestedPriority.HasValue
                ? $"Requested priority {priority} was applied."
                : "No priority requested; defaulted to Medium.";

        var now = clock.UtcNow;

        // SLA calculation using final priority and tier.
        var dueAtUtc = slaCalculator.CalculateDueAtUtc(
            priority,
            customer.Tier,
            now);

        var slaWindowHours = (dueAtUtc - now).TotalHours;

        var slaReason =
            $"SLA window is {slaWindowHours:0.##} hours " +
            $"for a {customer.Tier} customer " +
            $"with {priority} priority.";

        // Retrieve current agent workloads.
        var candidates = await agents.GetAssignmentCandidatesAsync(
            request.CategoryId,
            ct);

        var decision = agentSelector.Select(
            candidates,
            category.RequiresSpecialist);

        // Create a ticket with its final priority and due date.
        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority,
            now,
            dueAtUtc);

        string assignmentReason;

        if (decision.AgentId is { } agentId)
        {
            ticket.AssignTo(agentId, now);

            assignmentReason =
                $"Agent {agentId} selected because they are " +
                "eligible, active, below capacity and have " +
                "the lowest qualifying open-ticket workload " +
                "(using Agent ID to break ties).";
        }
        else
        {
            assignmentReason =
                decision.UnassignedReason
                ?? "No eligible agent is currently available.";

            logger.LogWarning(
                "Ticket {Reference} remains unassigned: {Reason}",
                ticket.Reference,
                assignmentReason);
        }

        // Persist ticket and assignment together.
        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Created ticket {Reference}. Priority: {Priority}; " +
            "Due: {DueAtUtc}; Agent: {AgentId}.",
            ticket.Reference,
            ticket.Priority,
            ticket.DueAtUtc,
            ticket.AssignedAgentId);

        // Read the saved ticket.
        var result = await getTicket.HandleAsync(
            ticket.Id,
            ct);

        // Explain the automatic triage decision.
        result.Triage = new TicketTriageDto(
            priorityReason,
            slaReason,
            assignmentReason);

        result.UnassignedReason = decision.UnassignedReason;

        return result;
    }
}
