
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class TicketTriageResponseTests
{
    [Fact]
    public async Task SecurityTicket_ReturnsCompleteTriageExplanation()
    {
        var context = new TicketCommandTestContext();

        var request = new CreateTicketRequest(
            "Security escalation verification",
            "Check priority, SLA and agent decision.",
            CustomerId: 1,
            CategoryId: 5,
            RequestedPriority: TicketPriority.Low);

        var result = await context.RaiseTicket.HandleAsync(
            request,
            CancellationToken.None);

        Assert.NotNull(result.Triage);

        Assert.Contains(
            "Critical",
            result.Triage.PriorityReason);

        Assert.Contains(
            "2",
            result.Triage.SlaReason);

        Assert.Contains(
            "Agent",
            result.Triage.AssignmentReason);

        Assert.NotNull(context.AddedTicket);

        Assert.Equal(
            TicketPriority.Critical,
            context.AddedTicket.Priority);

        Assert.Equal(
            3,
            context.AddedTicket.AssignedAgentId);

        Assert.Equal(
            context.Clock.UtcNow.AddHours(2),
            context.AddedTicket.DueAtUtc);

        Assert.Null(result.UnassignedReason);
    }

    [Fact]
    public async Task NoEligibleAgent_ReturnsTriageWithReason()
    {
        var context = new TicketCommandTestContext();

        context.Agents
            .Setup(a => a.GetAssignmentCandidatesAsync(
                5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (IReadOnlyList<AgentAssignmentCandidate>)
                Array.Empty<AgentAssignmentCandidate>());

        var request = new CreateTicketRequest(
            "Security assignment unavailable",
            "Ensure the ticket still gets created.",
            CustomerId: 1,
            CategoryId: 5,
            RequestedPriority: TicketPriority.Low);

        var result = await context.RaiseTicket.HandleAsync(
            request,
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        Assert.Null(context.AddedTicket.AssignedAgentId);

        Assert.NotNull(result.Triage);
        Assert.NotNull(result.UnassignedReason);

        Assert.Equal(
            result.UnassignedReason,
            result.Triage.AssignmentReason);

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task MissingRequestedPriority_ExplainsDefaultMedium()
    {
        var context = new TicketCommandTestContext();

        var request = new CreateTicketRequest(
            "Normal priority verification",
            "Check the default priority explanation.",
            CustomerId: 2,
            CategoryId: 3,
            RequestedPriority: null);

        var result = await context.RaiseTicket.HandleAsync(
            request,
            CancellationToken.None);

        Assert.NotNull(result.Triage);

        Assert.Contains(
            "defaulted to Medium",
            result.Triage.PriorityReason);

        Assert.NotNull(context.AddedTicket);

        Assert.Equal(
            TicketPriority.Medium,
            context.AddedTicket.Priority);

        Assert.Equal(
            context.Clock.UtcNow.AddHours(24),
            context.AddedTicket.DueAtUtc);
    }
}
