
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Data is projected in the database and never tracked.
/// Customer, category and agent labels are joined on the read side.
/// </summary>
public sealed class TicketQueries(
    SupportDbContext db,
    IClock clock) : ITicketQueries
{
    /// <summary>
    /// Returns tickets after SQL filtering, sorting and pagination.
    /// TotalCount is calculated after filtering but before paging.
    /// </summary>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(
        TicketQuery query,
        CancellationToken ct)
    {
        // Start with the original relational query.
        var tickets = TicketsWithLabels();

        // Apply every requested filter in the database.
        tickets = ApplyFilters(tickets, query);

        // Count matching tickets before Skip and Take.
        var totalCount = await tickets.CountAsync(ct);

        var now = clock.UtcNow;

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Tier),
                Category = new CategorySummaryDto(
                    x.Category.Id,
                    x.Category.Name),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(
                        x.Agent.Id,
                        x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }

    /// <summary>
    /// Applies optional filters using AND logic.
    /// The query stays IQueryable so filtering executes in SQL.
    /// </summary>
    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        // Status: exact match.
        if (query.Status is { } status)
        {
            source = source.Where(
                x => x.Ticket.Status == status);
        }

        // Priority: exact match.
        if (query.Priority is { } priority)
        {
            source = source.Where(
                x => x.Ticket.Priority == priority);
        }

        // Category ID: exact match.
        if (query.CategoryId is { } categoryId)
        {
            source = source.Where(
                x => x.Ticket.CategoryId == categoryId);
        }

        // Customer ID: exact match.
        if (query.CustomerId is { } customerId)
        {
            source = source.Where(
                x => x.Ticket.CustomerId == customerId);
        }

        // Assigned agent ID: exact match.
        if (query.AssignedAgentId is { } agentId)
        {
            source = source.Where(
                x => x.Ticket.AssignedAgentId == agentId);
        }

        // Show only tickets without an assigned agent.
        if (query.UnassignedOnly)
        {
            source = source.Where(
                x => x.Ticket.AssignedAgentId == null);
        }

        // Partial search across title, reference and customer name.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            // Escape LIKE wildcard characters so user-entered
            // text is treated literally.
            var escapedSearch = search
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");

            var pattern = $"%{escapedSearch}%";

            source = source.Where(x =>
                EF.Functions.Like(
                    x.Ticket.Title,
                    pattern,
                    @"\") ||
                EF.Functions.Like(
                    x.Ticket.Reference,
                    pattern,
                    @"\") ||
                EF.Functions.Like(
                    x.Customer.Name,
                    pattern,
                    @"\"));
        }

        // All successive Where clauses combine with AND logic.
        return source;
    }

    /// <summary>
    /// Returns a single ticket with its related labels.
    /// </summary>
    public Task<TicketDetailDto?> GetDetailAsync(
        int id,
        CancellationToken ct)
    {
        var now = clock.UtcNow;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Email,
                    x.Customer.Phone,
                    x.Customer.Tier),
                Category = new CategoryDto(
                    x.Category.Id,
                    x.Category.Name,
                    x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(
                        x.Agent.Id,
                        x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now)
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Returns all tickets belonging to one customer.
    /// </summary>
    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(
        int customerId,
        CancellationToken ct)
    {
        var now = clock.UtcNow;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Tier),
                Category = new CategorySummaryDto(
                    x.Category.Id,
                    x.Category.Name),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(
                        x.Agent.Id,
                        x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Joins tickets with customers, categories and optional agents.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>()
            on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>()
            on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>()
            .Where(a => a.Id == ticket.AssignedAgentId)
            .DefaultIfEmpty()
        select new TicketWithLabels
        {
            Ticket = ticket,
            Customer = customer,
            Category = category,
            Agent = agent
        };

    /// <summary>
    /// Applies sorting with a stable ticket ID tie-breaker.
    /// </summary>
    private static IQueryable<TicketWithLabels> ApplySort(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        var descending = !string.Equals(
            query.SortDirection,
            "asc",
            StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(
                    x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(
                    x => x.Ticket.UpdatedAtUtc),

            "dueatutc" => descending
                ? source.OrderByDescending(
                    x => x.Ticket.DueAtUtc)
                : source.OrderBy(
                    x => x.Ticket.DueAtUtc),

            "priority" => descending
                ? source.OrderByDescending(
                    x => x.Ticket.Priority)
                : source.OrderBy(
                    x => x.Ticket.Priority),

            "status" => descending
                ? source.OrderByDescending(
                    x => x.Ticket.Status)
                : source.OrderBy(
                    x => x.Ticket.Status),

            _ => descending
                ? source.OrderByDescending(
                    x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(
                    x => x.Ticket.CreatedAtUtc)
        };

        // Make pagination deterministic if sort values are equal.
        return sorted.ThenByDescending(
            x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
