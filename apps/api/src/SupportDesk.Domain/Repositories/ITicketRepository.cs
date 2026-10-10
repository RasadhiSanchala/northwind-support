using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Repositories;

/// <summary>Loads tracked ticket aggregates and escalation history.</summary>
public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(int id, CancellationToken ct);

    Task<string> NextReferenceAsync(CancellationToken ct);

    Task AddAsync(Ticket ticket, CancellationToken ct);

    /// <summary>Reads immutable escalation records newest first.</summary>
    Task<IReadOnlyList<TicketEscalation>> GetEscalationsAsync(int ticketId, CancellationToken ct);
}
