using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Domain.Tickets;

public sealed class SlaEvaluatorBoundaryTests
{
    private static readonly DateTime Start = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Due = Start.AddHours(8);

    [Fact]
    public void MissingDeadline_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable,
            SlaEvaluator.Evaluate(null, null, Start, Start, 25));

    [Fact]
    public void ResolvedAtDeadline_IsMet() =>
        Assert.Equal(SlaStatus.Met,
            SlaEvaluator.Evaluate(Due, Due, Due.AddDays(1), Start, 25));

    [Fact]
    public void ResolvedAfterDeadline_IsBreached() =>
        Assert.Equal(SlaStatus.Breached,
            SlaEvaluator.Evaluate(Due, Due.AddTicks(1), Start, Start, 25));

    [Fact]
    public void UnresolvedOneTickAfterDeadline_IsBreached() =>
        Assert.Equal(SlaStatus.Breached,
            SlaEvaluator.Evaluate(Due, null, Due.AddTicks(1), Start, 25));

    [Fact]
    public void MoreThanQuarterRemaining_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(Due, null, Start.AddHours(5).AddTicks(-1), Start, 25));

    [Fact]
    public void ExactlyQuarterRemaining_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(Due, null, Start.AddHours(6), Start, 25));

    [Fact]
    public void ExactlyAtDeadline_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(Due, null, Due, Start, 25));

    [Fact]
    public void EscalationReset_UsesNewStartTime() =>
        Assert.Equal(SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(Due.AddHours(4), null, Due, Due.AddHours(-2), 25));

    [Fact]
    public void ConfiguredThreshold_ChangesAtRiskBoundary() =>
        Assert.Equal(SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(Due, null, Start.AddHours(4), Start, 50));

    [Fact]
    public void InvalidThreshold_IsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SlaEvaluator.Evaluate(Due, null, Start, Start, 101));
}
