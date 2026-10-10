using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;
using Xunit;
using AppValidationException = SupportDesk.Application.Exceptions.ValidationException;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class TicketEscalationTests
{
    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Medium, TicketPriority.High, 4)]
    [InlineData(TicketPriority.High, TicketPriority.Critical, 2)]
    public async Task Escalation_RaisesExactlyOneStep_ResetsPremiumSlaFromNow(
        TicketPriority initial,
        TicketPriority expected,
        int hours)
    {
        var test = new TestHarness(initial);
        test.Clock.Advance(TimeSpan.FromHours(1));

        var result = await test.Handler.HandleAsync(
            test.Ticket.Id,
            new EscalateTicketRequest("Customer requested urgent help", "agent@example.com"),
            CancellationToken.None);

        Assert.Equal(expected, test.Ticket.Priority);
        Assert.Equal(test.Clock.UtcNow.AddHours(hours), test.Ticket.DueAtUtc);
        Assert.Equal(initial, result.Escalation.FromPriority);
        Assert.Equal(expected, result.Escalation.ToPriority);
        Assert.Equal(test.Clock.UtcNow, result.Escalation.EscalatedAtUtc);
        Assert.Single(test.Ticket.Escalations);
        test.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CurrentAgentAtCapacity_RemainsAssignedWhenStillQualified()
    {
        var test = new TestHarness(TicketPriority.Low);
        test.SetCandidates(
            new(2, true, 3, 3, false),
            new(1, true, 10, 0, false));

        var result = await test.EscalateAsync();

        Assert.Equal(2, test.Ticket.AssignedAgentId);
        Assert.Equal(2, result.Escalation.FromAgentId);
        Assert.Equal(2, result.Escalation.ToAgentId);
    }

    [Fact]
    public async Task InactiveAgent_IsReplacedWithLeastLoadedEligibleAgent()
    {
        var test = new TestHarness(TicketPriority.Low);
        test.SetCandidates(
            new(2, false, 8, 1, false),
            new(3, true, 8, 4, false),
            new(1, true, 8, 1, false));

        var result = await test.EscalateAsync();

        Assert.Equal(1, test.Ticket.AssignedAgentId);
        Assert.Equal(2, result.Escalation.FromAgentId);
        Assert.Equal(1, result.Escalation.ToAgentId);
    }

    [Fact]
    public async Task NoEligibleAgent_UnassignsWithoutFailing()
    {
        var test = new TestHarness(TicketPriority.Low);
        test.SetCandidates(new AgentAssignmentCandidate(2, false, 8, 1, false));

        var result = await test.EscalateAsync();

        Assert.Null(test.Ticket.AssignedAgentId);
        Assert.Equal(2, result.Escalation.FromAgentId);
        Assert.Null(result.Escalation.ToAgentId);
        test.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SpecialistCategory_ReassignsToQualifiedAgent()
    {
        var test = new TestHarness(TicketPriority.Low, requiresSpecialist: true);
        test.SetCandidates(
            new(2, true, 10, 2, false),
            new(3, true, 8, 4, true),
            new(5, true, 8, 1, true));

        var result = await test.EscalateAsync();

        Assert.Equal(5, test.Ticket.AssignedAgentId);
        Assert.Equal(5, result.Escalation.ToAgentId);
    }

    [Fact]
    public async Task CriticalTicket_IsRejectedWithoutWritingHistory()
    {
        var test = new TestHarness(TicketPriority.Critical);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => test.EscalateAsync());

        Assert.Contains("Critical", ex.Message);
        Assert.Empty(test.Ticket.Escalations);
        test.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task FinalStatus_IsRejectedWithoutWritingHistory(TicketStatus status)
    {
        var test = new TestHarness(TicketPriority.Low);
        test.Ticket.ChangeStatus(status, test.Clock.UtcNow);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => test.EscalateAsync());

        Assert.Contains(status.ToString(), ex.Message);
        Assert.Empty(test.Ticket.Escalations);
        test.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("no", "agent@example.com")]
    [InlineData("     ", "agent@example.com")]
    [InlineData("A valid reason", "")]
    public async Task InvalidReasonOrActor_IsValidationError(string reason, string actor)
    {
        var test = new TestHarness(TicketPriority.Low);

        await Assert.ThrowsAsync<AppValidationException>(() =>
            test.Handler.HandleAsync(test.Ticket.Id,
                new EscalateTicketRequest(reason, actor), CancellationToken.None));

        test.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HistoryQuery_ReturnsRepositoryResultsInOrder()
    {
        var test = new TestHarness(TicketPriority.Low);
        await test.EscalateAsync();
        test.Clock.Advance(TimeSpan.FromMinutes(3));
        await test.EscalateAsync();

        var newestFirst = test.Ticket.Escalations
            .OrderByDescending(e => e.EscalatedAtUtc)
            .ToArray();

        test.Tickets.Setup(r => r.GetEscalationsAsync(
                test.Ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newestFirst);

        var history = await new GetTicketEscalationsQueryHandler(test.Tickets.Object)
            .HandleAsync(test.Ticket.Id, CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.Equal(TicketPriority.High, history[0].ToPriority);
        Assert.Equal(TicketPriority.Medium, history[1].ToPriority);
    }

    private sealed class TestHarness
    {
        public readonly Mock<ITicketRepository> Tickets = new();
        public readonly Mock<ICustomerRepository> Customers = new();
        public readonly Mock<ICategoryRepository> Categories = new();
        public readonly Mock<IAgentRepository> Agents = new();
        public readonly Mock<IUnitOfWork> UnitOfWork = new();
        private readonly Mock<ITicketQueries> _queries = new();

        public readonly FixedClock Clock = new();
        public readonly Ticket Ticket;
        public readonly EscalateTicketCommandHandler Handler;

        public TestHarness(TicketPriority initialPriority, bool requiresSpecialist = false)
        {
            Ticket = Ticket.Raise(
                "TCK-TEST", "Escalation unit test", "Testing escalation business rules.",
                customerId: 1, categoryId: 3, initialPriority,
                Clock.UtcNow, Clock.UtcNow.AddHours(36));
            Ticket.AssignTo(2, Clock.UtcNow);

            Tickets.Setup(r => r.GetByIdAsync(
                    Ticket.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Ticket);

            Customers.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Customer("Contoso Ltd", "contoso@example.com",
                    null, CustomerTier.Premium, Clock.UtcNow));

            Categories.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Category("Technical", requiresSpecialist,
                    forcesCriticalPriority: false));

            SetCandidates(
                new(2, true, 8, 2, requiresSpecialist),
                new(1, true, 10, 1, requiresSpecialist));

            _queries.Setup(q => q.GetDetailAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TicketDetailDto { Reference = "TCK-TEST" });

            var options = new SlaOptions
            {
                BaseWindowHours = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Low"] = 72, ["Medium"] = 24, ["High"] = 8, ["Critical"] = 4
                },
                PremiumCustomerMultiplier = 0.5,
                MinimumWindowHours = 1,
                AtRiskThresholdPercent = 25
            };

            Handler = new EscalateTicketCommandHandler(
                Tickets.Object, Customers.Object, Categories.Object, Agents.Object,
                UnitOfWork.Object, Clock, new SlaDeadlineCalculator(options),
                new AutomaticAgentSelector(),
                new GetTicketQueryHandler(_queries.Object),
                NullLogger<EscalateTicketCommandHandler>.Instance);
        }

        public void SetCandidates(params AgentAssignmentCandidate[] candidates) =>
            Agents.Setup(r => r.GetAssignmentCandidatesAsync(
                    3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(candidates);

        public Task<EscalateTicketResponse> EscalateAsync() => Handler.HandleAsync(
            Ticket.Id,
            new EscalateTicketRequest("Customer requires urgent handling", "agent@example.com"),
            CancellationToken.None);
    }
}
