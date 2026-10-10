using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Before login is implemented, the caller supplies EscalatedBy.
/// Task 3 will take the actor from the authenticated JWT instead.
/// </summary>
public sealed record EscalateTicketRequest(string Reason, string EscalatedBy);

/// <summary>Immutable before-and-after escalation snapshot.</summary>
public sealed record TicketEscalationDto(
    int Id,
    int TicketId,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    int? FromAgentId,
    int? ToAgentId,
    DateTime? FromDueAtUtc,
    DateTime? ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc)
{
    public static TicketEscalationDto From(TicketEscalation escalation) => new(
        escalation.Id,
        escalation.TicketId,
        escalation.FromPriority,
        escalation.ToPriority,
        escalation.FromAgentId,
        escalation.ToAgentId,
        escalation.FromDueAtUtc,
        escalation.ToDueAtUtc,
        escalation.Reason,
        escalation.EscalatedBy,
        escalation.EscalatedAtUtc);
}

public sealed record EscalateTicketResponse(
    TicketDetailDto Ticket,
    TicketEscalationDto Escalation);
