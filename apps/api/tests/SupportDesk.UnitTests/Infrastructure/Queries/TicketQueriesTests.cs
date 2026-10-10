
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly TicketQueries _queries;

    private readonly int _billingId;
    private readonly int _outageId;
    private readonly int _northwindId;
    private readonly int _agentId;

    public TicketQueriesTests()
    {
        // Create an isolated SQLite database for each test.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        var now = FixedClock.DefaultNow;

        // Customers
        var aster = new Customer(
            "Aster Holdings",
            "support@aster.example",
            null,
            CustomerTier.Standard,
            now);

        var northwind = new Customer(
            "Northwind Labs",
            "support@northwind.example",
            null,
            CustomerTier.Premium,
            now);

        // Categories
        var billing = new Category(
            "Billing",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        var outage = new Category(
            "Outage",
            requiresSpecialist: true,
            forcesCriticalPriority: true);

        // Agent
        var agent = new Agent(
            "Alex Turner",
            "alex@example.com",
            maxOpenTickets: 10,
            now);

        _db.AddRange(
            aster,
            northwind,
            billing,
            outage,
            agent);

        _db.SaveChanges();

        _billingId = billing.Id;
        _outageId = outage.Id;
        _northwindId = northwind.Id;
        _agentId = agent.Id;

        // Ticket 1: Critical / New / Billing / Aster
        var first = Ticket.Raise(
            "TCK-0001",
            "Invoice outage alert",
            "A billing service is unavailable.",
            aster.Id,
            billing.Id,
            TicketPriority.Critical,
            now);

        // Ticket 2: High / InProgress / Billing / Aster
        var second = Ticket.Raise(
            "TCK-0002",
            "Delayed invoice",
            "An invoice has not arrived.",
            aster.Id,
            billing.Id,
            TicketPriority.High,
            now);

        second.AssignTo(agent.Id, now);
        second.ChangeStatus(TicketStatus.InProgress, now);

        // Ticket 3: Medium / New / Outage / Northwind
        var third = Ticket.Raise(
            "TCK-0003",
            "Router unreachable",
            "The customer router is not responding.",
            northwind.Id,
            outage.Id,
            TicketPriority.Medium,
            now);

        // Ticket 4: Critical / New / Outage / Northwind
        var fourth = Ticket.Raise(
            "TCK-0004",
            "Security incident",
            "A security incident needs investigation.",
            northwind.Id,
            outage.Id,
            TicketPriority.Critical,
            now);

        // Ticket 5: Low / New / Billing / Northwind
        var fifth = Ticket.Raise(
            "TCK-0005",
            "Payment receipt",
            "A payment receipt is missing.",
            northwind.Id,
            billing.Id,
            TicketPriority.Low,
            now);

        _db.Set<Ticket>().AddRange(
            first,
            second,
            third,
            fourth,
            fifth);

        _db.SaveChanges();

        _queries = new TicketQueries(_db, new FixedClock());
    }

    // TEST 1: Priority filtering
    [Fact]
    public async Task PriorityFilter_ReturnsOnlyMatchingTickets()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.Critical
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        Assert.All(
            result.Items,
            ticket => Assert.Equal(
                TicketPriority.Critical,
                ticket.Priority));
    }

    // TEST 2: Combined filters (AND)
    [Fact]
    public async Task CombinedFilters_UseAndLogic()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Status = TicketStatus.New,
                Priority = TicketPriority.Critical,
                CategoryId = _outageId,
                CustomerId = _northwindId
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-0004", ticket.Reference);
    }

    // TEST 3: Customer name search
    [Fact]
    public async Task Search_MatchesCustomerName()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Search = "northwind"
            },
            CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);

        Assert.All(
            result.Items,
            ticket => Assert.Equal(
                "Northwind Labs",
                ticket.Customer.Name));
    }

    // TESTS 4 and 5: Title and reference search
    [Theory]
    [InlineData("router", "TCK-0003")]
    [InlineData("TCK-0002", "TCK-0002")]
    public async Task Search_MatchesTitleOrReference(
        string search,
        string expectedReference)
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Search = search
            },
            CancellationToken.None);

        var ticket = Assert.Single(result.Items);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(expectedReference, ticket.Reference);
    }

    // TEST 6: Filter count before pagination
    [Fact]
    public async Task Pagination_CountsFilteredResultsBeforePaging()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.Critical,
                Page = 1,
                PageSize = 1
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
    }

    // TEST 7: Category filter
    [Fact]
    public async Task CategoryFilter_ReturnsOnlyMatchingCategory()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                CategoryId = _billingId
            },
            CancellationToken.None);

        Assert.Equal(3, result.TotalCount);

        Assert.All(
            result.Items,
            ticket => Assert.Equal(
                _billingId,
                ticket.Category.Id));
    }

    // TEST 8: Assigned agent filter
    [Fact]
    public async Task AssignedAgentFilter_ReturnsOnlyAssignedTickets()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                AssignedAgentId = _agentId
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-0002", ticket.Reference);
    }

    // TEST 9: Unassigned tickets
    [Fact]
    public async Task UnassignedOnly_ReturnsTicketsWithoutAgents()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery
            {
                UnassignedOnly = true
            },
            CancellationToken.None);

        Assert.Equal(4, result.TotalCount);

        Assert.All(
            result.Items,
            ticket => Assert.Null(ticket.AssignedAgent));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
