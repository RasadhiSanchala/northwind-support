
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer.
/// Every change to a ticket goes through its domain methods.
/// </summary>
public sealed class Ticket : AggregateRoot
{
    private Ticket()
    {
        // Required by EF Core.
    }

    /// <summary>
    /// Human-friendly identifier, e.g. TCK-0042.
    /// </summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>
    /// Null while the ticket has no assigned agent.
    /// </summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// SLA response deadline. Null when no SLA has been assigned.
    /// </summary>
    public DateTime? DueAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>
    /// True while the ticket still requires agent attention.
    /// </summary>
    public bool IsOpen =>
        Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// Creates a ticket with its calculated SLA deadline.
    /// The optional deadline preserves compatibility with
    /// existing domain tests and callers.
    /// </summary>
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

    /// <summary>
    /// Changes ticket status while maintaining
    /// its resolution timestamp.
    /// </summary>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed &&
            status != TicketStatus.Closed)
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

    /// <summary>
    /// Assigns or unassigns an agent.
    /// The caller validates agent eligibility.
    /// </summary>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }
}
