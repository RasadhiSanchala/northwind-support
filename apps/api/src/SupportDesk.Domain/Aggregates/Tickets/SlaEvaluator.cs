namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Calculates SLA status from timestamps. Status is never stored in the database.
/// Escalations reset the SLA start; callers must supply the latest escalation time
/// or the original ticket creation time when using the five-argument overload.
/// </summary>
public static class SlaEvaluator
{
    /// <summary>Compatibility overload for existing callers that have no SLA start.</summary>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc) =>
        dueAtUtc is null ? SlaStatus.NotApplicable
        : resolvedAtUtc is not null
            ? (resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
        : nowUtc > dueAtUtc ? SlaStatus.Breached
        : SlaStatus.WithinSla;

    /// <summary>
    /// AtRisk begins when remaining time is 25% or less of the SLA window
    /// (or the configured percentage). Exactly at the due date is AtRisk;
    /// a ticket is Breached only after the deadline has passed.
    /// </summary>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime slaStartedAtUtc,
        double atRiskThresholdPercent)
    {
        if (!double.IsFinite(atRiskThresholdPercent) ||
            atRiskThresholdPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(atRiskThresholdPercent));
        }

        if (dueAtUtc is null)
            return SlaStatus.NotApplicable;

        if (resolvedAtUtc is not null)
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;

        if (nowUtc > dueAtUtc)
            return SlaStatus.Breached;

        var windowTicks = dueAtUtc.Value.Ticks - slaStartedAtUtc.Ticks;
        if (windowTicks <= 0)
            return SlaStatus.WithinSla;

        var remainingTicks = dueAtUtc.Value.Ticks - nowUtc.Ticks;
        return remainingTicks * 100.0 <= windowTicks * atRiskThresholdPercent
            ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;
    }
}
