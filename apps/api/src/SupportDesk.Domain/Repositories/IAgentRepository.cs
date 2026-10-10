
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Information needed to decide whether an agent
/// can receive a new support ticket.
/// </summary>
public sealed record AgentAssignmentCandidate(
    int AgentId,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    bool IsSpecialist);

/// <summary>
/// Loads agents and their assignment-related information.
/// </summary>
public interface IAgentRepository
{
    /// <summary>
    /// Loads an agent, including their specializations.
    /// </summary>
    Task<Agent?> GetByIdAsync(
        int id,
        CancellationToken ct);

    /// <summary>
    /// Returns agent workload and specialization information
    /// for the requested ticket category.
    ///
    /// OpenTicketCount excludes Resolved and Closed tickets.
    /// IsSpecialist indicates whether the agent has the
    /// requested category specialization.
    ///
    /// Eligibility and deterministic selection are
    /// handled by the application layer.
    /// </summary>
    Task<IReadOnlyList<AgentAssignmentCandidate>>
        GetAssignmentCandidatesAsync(
            int categoryId,
            CancellationToken ct);
}
