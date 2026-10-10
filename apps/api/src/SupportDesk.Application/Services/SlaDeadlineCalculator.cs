
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

/// <summary>
/// Calculates the response deadline for a support ticket.
/// </summary>
public sealed class SlaDeadlineCalculator(SlaOptions options)
{
    public DateTime CalculateDueAtUtc(
        TicketPriority priority,
        CustomerTier customerTier,
        DateTime startsAtUtc)
    {
        // Read the base SLA window from configuration.
        if (!options.BaseWindowHours.TryGetValue(
                priority.ToString(),
                out var baseHours) ||
            !double.IsFinite(baseHours) ||
            baseHours <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid SLA window for priority {priority}.");
        }

        // Premium customers receive half the standard
        // response window, unless configuration says otherwise.
        var multiplier = customerTier == CustomerTier.Premium
            ? options.PremiumCustomerMultiplier
            : 1.0;

        if (!double.IsFinite(multiplier) || multiplier <= 0)
        {
            throw new InvalidOperationException(
                "Invalid SLA customer multiplier.");
        }

        if (!double.IsFinite(options.MinimumWindowHours) ||
            options.MinimumWindowHours <= 0)
        {
            throw new InvalidOperationException(
                "Invalid SLA minimum response window.");
        }

        // Never schedule a response window shorter than
        // the configured minimum.
        var windowHours = Math.Max(
            options.MinimumWindowHours,
            baseHours * multiplier);

        return startsAtUtc.AddHours(windowHours);
    }
}
