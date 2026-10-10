
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(
        TicketPriority? priority = null,
        int categoryId = 3,
        int customerId = 1) =>
        new(
            "Cannot log in to the portal",
            "Happens for every user since this morning.",
            customerId,
            categoryId,
            priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.High),
            CancellationToken.None);

        var created = context.AddedTicket;

        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        Assert.Equal(
            TicketPriority.Medium,
            context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        context.Customers
            .Setup(c => c.ExistsAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(
                Request(),
                CancellationToken.None));

        Assert.Contains("Customer", exception.Message);

        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public async Task HandleAsync_ForcedCriticalCategory_OverridesLowPriority(
        int categoryId)
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(
                TicketPriority.Low,
                categoryId),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        Assert.Equal(
            TicketPriority.Critical,
            context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_NormalCategory_PreservesRequestedPriority()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(
                TicketPriority.Low,
                categoryId: 3),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        Assert.Equal(
            TicketPriority.Low,
            context.AddedTicket.Priority);
    }

    // SLA TESTS
    // Customer ID 1: Premium
    // Customer ID 2: Standard
    // Category ID 3: Technical (does not force Critical)

    [Theory]
    [InlineData(TicketPriority.Critical, 2, 4)]
    [InlineData(TicketPriority.High, 2, 8)]
    [InlineData(TicketPriority.Medium, 2, 24)]
    [InlineData(TicketPriority.Low, 2, 72)]
    [InlineData(TicketPriority.Critical, 1, 2)]
    [InlineData(TicketPriority.High, 1, 4)]
    [InlineData(TicketPriority.Medium, 1, 12)]
    [InlineData(TicketPriority.Low, 1, 36)]
    public async Task HandleAsync_CalculatesDueDateByPriorityAndCustomerTier(
        TicketPriority priority,
        int customerId,
        int expectedHours)
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(
                priority: priority,
                categoryId: 3,
                customerId: customerId),
            CancellationToken.None);

        var created = context.AddedTicket;

        Assert.NotNull(created);
        Assert.NotNull(created.DueAtUtc);

        var expectedDueDate = context.Clock.UtcNow.AddHours(
            expectedHours);

        Assert.Equal(
            expectedDueDate,
            created.DueAtUtc.Value);
    }

    [Fact]
    public async Task HandleAsync_ForcedCriticalCategory_UsesCriticalSla()
    {
        var context = new TicketCommandTestContext();

        // Premium customer requests Low priority.
        // Security category forces Critical.
        // Therefore, SLA must be 2 hours, not 36 hours.

        await context.RaiseTicket.HandleAsync(
            Request(
                priority: TicketPriority.Low,
                categoryId: 5,
                customerId: 1),
            CancellationToken.None);

        var created = context.AddedTicket;

        Assert.NotNull(created);

        Assert.Equal(
            TicketPriority.Critical,
            created.Priority);

        Assert.NotNull(created.DueAtUtc);

        Assert.Equal(
            context.Clock.UtcNow.AddHours(2),
            created.DueAtUtc.Value);
    }
}
