
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

/// <summary>
/// Loads agents and retrieves the information needed
/// for automatic ticket assignment.
/// </summary>
public sealed class AgentRepository(
    SupportDbContext db) : IAgentRepository
{
    /// <summary>
    /// Loads an agent with their specializations.
    /// Existing manual assignment functionality uses this.
    /// </summary>
    public Task<Agent?> GetByIdAsync(
        int id,
        CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>
    /// Retrieves workload, capacity and specialization
    /// information for every agent.
    ///
    /// Resolved and Closed tickets do not count toward
    /// an agent's current workload.
    ///
    /// The application layer selects the best
    /// eligible candidate.
    /// </summary>
    public async Task<IReadOnlyList<AgentAssignmentCandidate>>
        GetAssignmentCandidatesAsync(
            int categoryId,
            CancellationToken ct)
    {
        var candidates = await db.Set<Agent>()
            .AsNoTracking()
            .Select(agent => new AgentAssignmentCandidate(
                agent.Id,
                agent.IsActive,
                agent.MaxOpenTickets,

                // Count only tickets still needing work.
                db.Set<Ticket>().Count(ticket =>
                    ticket.AssignedAgentId == agent.Id &&
                    ticket.Status != TicketStatus.Resolved &&
                    ticket.Status != TicketStatus.Closed),

                // Determine qualification for this category.
                agent.Specializations.Any(s =>
                    s.CategoryId == categoryId)
            ))
            .ToListAsync(ct);

        return candidates;
    }
}
