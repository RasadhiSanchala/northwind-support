using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

/// <summary>Returns a ticket's escalation history, newest first.</summary>
public sealed class GetTicketEscalationsQueryHandler(ITicketRepository tickets)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(
        int ticketId, CancellationToken ct)
    {
        _ = await tickets.GetByIdAsync(ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);

        var history = await tickets.GetEscalationsAsync(ticketId, ct);
        return history.Select(TicketEscalationDto.From).ToArray();
    }
}
