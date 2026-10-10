
namespace SupportDesk.Application.Services;

/// <summary>
/// SLA rules loaded from application configuration.
/// </summary>
public sealed class SlaOptions
{
    public Dictionary<string, double> BaseWindowHours
    {
        get;
        set;
    } = new(StringComparer.OrdinalIgnoreCase);

    public double PremiumCustomerMultiplier
    {
        get;
        set;
    } = 0.5;

    public double MinimumWindowHours
    {
        get;
        set;
    } = 1;

    public double AtRiskThresholdPercent
    {
        get;
        set;
    } = 25;
}
