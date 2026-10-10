using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>A support request raised by a customer. Changes go through domain methods.</summary>
public sealed class Ticket : AggregateRoot
{
    private readonly List<TicketEscalation> _escalations = [];

    private Ticket() { /* Required by EF Core. */ }

    public string Reference { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int CustomerId { get; private set; }
    public int CategoryId { get; private set; }
    public int? AssignedAgentId { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? DueAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>Escalation records cannot be edited or removed through the aggregate.</summary>
    public IReadOnlyCollection<TicketEscalation> Escalations => _escalations.AsReadOnly();

    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc,
        DateTime? dueAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (dueAtUtc.HasValue && dueAtUtc.Value <= nowUtc)
        {
            throw new BusinessRuleViolationException(
                "The SLA deadline must be later than ticket creation.");
        }

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            DueAtUtc = dueAtUtc
        };
    }

    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Moves exactly one priority level, resets the SLA clock, and records the immutable
    /// before/after state atomically with the ticket update.
    /// </summary>
    public TicketEscalation Escalate(
        string reason,
        string escalatedBy,
        DateTime nowUtc,
        DateTime newDueAtUtc,
        int? newAgentId)
    {
        if (Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} has status {Status} and cannot be escalated.");
        }

        if (Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is already Critical and cannot be escalated.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(escalatedBy);
        reason = reason.Trim();
        escalatedBy = escalatedBy.Trim();

        if (reason.Length is < 5 or > 500)
            throw new ArgumentException("Reason must contain 5 to 500 characters.", nameof(reason));

        if (escalatedBy.Length > 100)
            throw new ArgumentException("EscalatedBy cannot exceed 100 characters.", nameof(escalatedBy));

        if (newDueAtUtc <= nowUtc)
            throw new BusinessRuleViolationException("The new SLA deadline must be in the future.");

        var nextPriority = (TicketPriority)((int)Priority + 1);
        var history = new TicketEscalation(
            Priority,
            nextPriority,
            AssignedAgentId,
            newAgentId,
            DueAtUtc,
            newDueAtUtc,
            reason,
            escalatedBy,
            nowUtc);

        Priority = nextPriority;
        AssignedAgentId = newAgentId;
        DueAtUtc = newDueAtUtc;
        UpdatedAtUtc = nowUtc;
        _escalations.Add(history);

        return history;
    }
}
