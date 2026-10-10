
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Services;

/// <summary>
/// Result of automatic ticket assignment.
/// An agent may be unavailable, in which case
/// the ticket remains unassigned with a reason.
/// </summary>
public sealed record AgentAssignmentDecision(
    int? AgentId,
    string? UnassignedReason);

/// <summary>
/// Selects the most suitable agent for a ticket.
/// </summary>
public sealed class AutomaticAgentSelector
{
    public AgentAssignmentDecision Select(
        IReadOnlyList<AgentAssignmentCandidate> candidates,
        bool requiresSpecialist)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        // Only active agents can receive new tickets.
        var activeAgents = candidates
            .Where(agent => agent.IsActive);

        // Agents must have available capacity.
        var availableAgents = activeAgents
            .Where(agent =>
                agent.OpenTicketCount < agent.MaxOpenTickets);

        // Specialist categories require qualification.
        // Normal categories do not require specialization.
        var eligibleAgents = availableAgents
            .Where(agent =>
                !requiresSpecialist || agent.IsSpecialist);

        // Choose the least-loaded eligible agent.
        // If workload is equal, choose the lowest agent ID
        // so selection remains deterministic.
        var selectedAgent = eligibleAgents
            .OrderBy(agent => agent.OpenTicketCount)
            .ThenBy(agent => agent.AgentId)
            .FirstOrDefault();

        if (selectedAgent is not null)
        {
            return new AgentAssignmentDecision(
                selectedAgent.AgentId,
                UnassignedReason: null);
        }

        // Creating a ticket must still succeed when
        // no eligible agent is available.
        var reason = requiresSpecialist
            ? "No eligible active specialist agent with available capacity."
            : "No eligible active agent with available capacity.";

        return new AgentAssignmentDecision(
            AgentId: null,
            UnassignedReason: reason);
    }
}
