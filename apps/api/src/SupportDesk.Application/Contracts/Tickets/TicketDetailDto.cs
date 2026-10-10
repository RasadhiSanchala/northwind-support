
using System.Text.Json.Serialization;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Explains automatic decisions made during ticket creation.
/// </summary>
public sealed record TicketTriageDto(
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);

/// <summary>
/// Details of a support ticket, including customer,
/// category, SLA and assignment information.
/// </summary>
public sealed class TicketDetailDto
{
    public int Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TicketStatus Status { get; set; }

    public TicketPriority Priority { get; set; }

    public CustomerContactDto Customer { get; set; } = null!;

    public CategoryDto Category { get; set; } = null!;

    public AgentSummaryDto? AssignedAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>
    /// Calculated SLA status; never stored directly.
    /// </summary>
    public SlaStatus SlaStatus { get; set; }

    /// <summary>
    /// Present in the creation response to explain
    /// automatic priority, SLA and agent decisions.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TicketTriageDto? Triage { get; set; }

    /// <summary>
    /// Present when automatic assignment finds no agent.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UnassignedReason { get; set; }
}
