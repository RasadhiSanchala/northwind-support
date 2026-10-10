using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Exercises SQL filtering/count/paging for provider-neutral SLA predicates.
/// AtRisk/WithinSla SQL Server DateDiff branches are verified with the live API
/// smoke script; pure timing boundaries are covered by SlaEvaluatorBoundaryTests.
/// </summary>
public sealed class SlaFilterQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly TicketQueries _queries;

    public SlaFilterQueryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>()
                .UseSqlite(_connection)
                .Options);
        _db.Database.EnsureCreated();

        var now = FixedClock.DefaultNow;
        var customer = new Customer("Acme", "help@acme.example", null,
            CustomerTier.Standard, now);
        var category = new Category("Technical", false, false);
        _db.AddRange(customer, category);
        _db.SaveChanges();

        var withoutSla = Ticket.Raise("SLA-001", "No SLA", "Legacy ticket without SLA",
            customer.Id, category.Id, TicketPriority.Low, now);
        var met = Ticket.Raise("SLA-002", "Resolved in time", "Successful ticket",
            customer.Id, category.Id, TicketPriority.High, now, now.AddHours(2));
        met.ChangeStatus(TicketStatus.Resolved, now.AddHours(1));
        var late = Ticket.Raise("SLA-003", "Resolved too late", "Late ticket",
            customer.Id, category.Id, TicketPriority.High, now, now.AddHours(2));
        late.ChangeStatus(TicketStatus.Resolved, now.AddHours(3));
        var overdue = Ticket.Raise("SLA-004", "Still overdue", "Unresolved late ticket",
            customer.Id, category.Id, TicketPriority.Low, now.AddHours(-5), now.AddHours(-1));

        _db.Set<Ticket>().AddRange(withoutSla, met, late, overdue);
        _db.SaveChanges();
        _queries = new TicketQueries(_db, new FixedClock());
    }

    [Theory]
    [InlineData(SlaStatus.NotApplicable, 1)]
    [InlineData(SlaStatus.Met, 1)]
    [InlineData(SlaStatus.Breached, 2)]
    public async Task SlaFilter_AppliedBeforeCountAndPaging(SlaStatus state, int expected)
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            SlaStatus = state,
            Page = 1,
            PageSize = 1
        }, CancellationToken.None);

        Assert.Equal(expected, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(state, result.Items[0].SlaStatus);
    }

    [Fact]
    public async Task SlaAndPriorityFilters_CombineWithAnd()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            SlaStatus = SlaStatus.Breached,
            Priority = TicketPriority.High
        }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("SLA-003", Assert.Single(result.Items).Reference);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
