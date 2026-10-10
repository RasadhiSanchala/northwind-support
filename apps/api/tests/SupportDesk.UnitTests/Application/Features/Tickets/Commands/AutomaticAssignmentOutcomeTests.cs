
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class AutomaticAssignmentOutcomeTests
{
    private static CreateTicketRequest Request(
        int categoryId) =>
        new(
            "Automatic assignment outcome verification",
            "Verify agent selection and unassigned reason.",
            CustomerId: 1,
            CategoryId: categoryId,
            RequestedPriority: TicketPriority.Medium);

    [Fact]
    public async Task NoEligibleSpecialist_CreatesTicketWithReason()
    {
        var context = new TicketCommandTestContext();

        // No agents are available for Security.
        context.Agents
            .Setup(a => a.GetAssignmentCandidatesAsync(
                5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (IReadOnlyList<AgentAssignmentCandidate>)
                Array.Empty<AgentAssignmentCandidate>());

        var result = await context.RaiseTicket.HandleAsync(
            Request(categoryId: 5),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        Assert.Null(context.AddedTicket.AssignedAgentId);

        Assert.Equal(
            TicketPriority.Critical,
            context.AddedTicket.Priority);

        Assert.NotNull(result.UnassignedReason);

        Assert.Contains(
            "specialist",
            result.UnassignedReason,
            StringComparison.OrdinalIgnoreCase);

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task NoAvailableAgent_CreatesNormalTicketWithReason()
    {
        var context = new TicketCommandTestContext();

        // No agents available for Technical.
        context.Agents
            .Setup(a => a.GetAssignmentCandidatesAsync(
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (IReadOnlyList<AgentAssignmentCandidate>)
                Array.Empty<AgentAssignmentCandidate>());

        var result = await context.RaiseTicket.HandleAsync(
            Request(categoryId: 3),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        Assert.Null(context.AddedTicket.AssignedAgentId);

        Assert.NotNull(result.UnassignedReason);

        Assert.Contains(
            "agent",
            result.UnassignedReason,
            StringComparison.OrdinalIgnoreCase);

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task EligibleAgent_AssignsWithoutUnassignedReason()
    {
        var context = new TicketCommandTestContext();

        var result = await context.RaiseTicket.HandleAsync(
            Request(categoryId: 3),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        Assert.Equal(
            1,
            context.AddedTicket.AssignedAgentId);

        Assert.Null(result.UnassignedReason);

        context.VerifySaved(Times.Once());
    }
}
