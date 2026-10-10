
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <summary>
/// Regression tests for automatic agent assignment
/// when a new ticket is created.
/// </summary>
public class AutomaticAssignmentOnCreationTests
{
    private static CreateTicketRequest Request(
        int categoryId) =>
        new(
            "Automatic assignment verification",
            "Verify that an eligible agent receives the ticket.",
            CustomerId: 1,
            CategoryId: categoryId,
            RequestedPriority: TicketPriority.Medium);

    [Fact]
    public async Task HandleAsync_NormalCategory_AssignsEligibleAgent()
    {
        var context = new TicketCommandTestContext();

        // Technical category does not require a specialist.
        // The test agent pool will be configured in
        // the GREEN implementation stage.

        await context.RaiseTicket.HandleAsync(
            Request(categoryId: 3),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        Assert.Equal(
            1,
            context.AddedTicket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_SpecialistCategory_AssignsQualifiedAgent()
    {
        var context = new TicketCommandTestContext();

        // Security requires a specialist.
        // Agent 3 will be configured as an eligible
        // Security specialist in the test fixture.

        await context.RaiseTicket.HandleAsync(
            Request(categoryId: 5),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        Assert.Equal(
            TicketPriority.Critical,
            context.AddedTicket.Priority);

        Assert.Equal(
            3,
            context.AddedTicket.AssignedAgentId);
    }
}
