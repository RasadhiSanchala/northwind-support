
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <summary>
/// Shared test context with mocked repositories,
/// fixed clock and ticket command handlers.
/// </summary>
internal sealed class TicketCommandTestContext
{
    public TicketCommandTestContext()
    {
        // Customer existence validation.
        Customers
            .Setup(c => c.ExistsAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Premium customer.
        Customers
            .Setup(c => c.GetByIdAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer(
                "Contoso Ltd",
                "support@contoso.example",
                null,
                CustomerTier.Premium,
                Clock.UtcNow));

        // Standard customer.
        Customers
            .Setup(c => c.GetByIdAsync(
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer(
                "Fabrikam Inc",
                "helpdesk@fabrikam.example",
                null,
                CustomerTier.Standard,
                Clock.UtcNow));

        // Category existence validation.
        Categories
            .Setup(c => c.ExistsAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Technical: No specialist required.
        Categories
            .Setup(c => c.GetByIdAsync(
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(
                "Technical",
                requiresSpecialist: false,
                forcesCriticalPriority: false));

        // Outage: Specialist required, Critical priority.
        Categories
            .Setup(c => c.GetByIdAsync(
                4,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(
                "Outage",
                requiresSpecialist: true,
                forcesCriticalPriority: true));

        // Security: Specialist required, Critical priority.
        Categories
            .Setup(c => c.GetByIdAsync(
                5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(
                "Security",
                requiresSpecialist: true,
                forcesCriticalPriority: true));

        // Technical candidates.
        // Agent 1 has fewer open tickets than Agent 2.
        Agents
            .Setup(a => a.GetAssignmentCandidatesAsync(
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (IReadOnlyList<AgentAssignmentCandidate>)
                new List<AgentAssignmentCandidate>
                {
                    new(1, true, 10, 1, false),
                    new(2, true, 8, 3, false),
                    new(6, false, 8, 0, true)
                });

        // Outage candidates.
        // Both agents are specialists, but Agent 2
        // has fewer open tickets.
        Agents
            .Setup(a => a.GetAssignmentCandidatesAsync(
                4,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (IReadOnlyList<AgentAssignmentCandidate>)
                new List<AgentAssignmentCandidate>
                {
                    new(2, true, 8, 0, true),
                    new(3, true, 8, 2, true)
                });

        // Security candidates.
        // Agent 1 is not qualified.
        // Agent 5 is at full capacity.
        // Agent 3 is the only eligible specialist.
        Agents
            .Setup(a => a.GetAssignmentCandidatesAsync(
                5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (IReadOnlyList<AgentAssignmentCandidate>)
                new List<AgentAssignmentCandidate>
                {
                    new(1, true, 10, 0, false),
                    new(3, true, 8, 2, true),
                    new(5, true, 6, 6, true)
                });

        // Reference generator.
        Tickets
            .Setup(t => t.NextReferenceAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("TCK-0041");

        // Capture newly created tickets for assertions.
        Tickets
            .Setup(t => t.AddAsync(
                It.IsAny<Ticket>(),
                It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>(
                (ticket, _) => AddedTicket = ticket)
            .Returns(Task.CompletedTask);

        // Mock ticket detail lookup after saving.
        TicketQueries
            .Setup(q => q.GetDetailAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto
            {
                Id = 41,
                Reference = "TCK-0041",
                Customer = new CustomerContactDto(
                    1,
                    "Contoso Ltd",
                    "support@contoso.example",
                    null,
                    CustomerTier.Premium),
                Category = new CategoryDto(
                    1,
                    "General",
                    false)
            });

        var getTicket = new GetTicketQueryHandler(
            TicketQueries.Object);

        // SLA calculator with deterministic test settings.
        var slaOptions = new SlaOptions
        {
            BaseWindowHours = new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["Low"] = 72,
                ["Medium"] = 24,
                ["High"] = 8,
                ["Critical"] = 4
            },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1,
            AtRiskThresholdPercent = 25
        };

        var slaCalculator = new SlaDeadlineCalculator(
            slaOptions);

        var agentSelector = new AutomaticAgentSelector();

        // Ticket creation with automatic SLA and assignment.
        RaiseTicket = new RaiseTicketCommandHandler(
            Tickets.Object,
            Customers.Object,
            Categories.Object,
            Agents.Object,
            UnitOfWork.Object,
            Clock,
            getTicket,
            slaCalculator,
            agentSelector,
            NullLogger<RaiseTicketCommandHandler>.Instance);

        ChangeTicketStatus = new ChangeTicketStatusCommandHandler(
            Tickets.Object,
            UnitOfWork.Object,
            Clock,
            getTicket);

        AssignTicket = new AssignTicketCommandHandler(
            Tickets.Object,
            Agents.Object,
            UnitOfWork.Object,
            Clock,
            getTicket,
            NullLogger<AssignTicketCommandHandler>.Instance);
    }

    public Mock<ITicketRepository> Tickets { get; } = new();

    public Mock<ICustomerRepository> Customers { get; } = new();

    public Mock<ICategoryRepository> Categories { get; } = new();

    public Mock<IAgentRepository> Agents { get; } = new();

    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public Mock<ITicketQueries> TicketQueries { get; } = new();

    public FixedClock Clock { get; } = new();

    public RaiseTicketCommandHandler RaiseTicket { get; }

    public ChangeTicketStatusCommandHandler ChangeTicketStatus { get; }

    public AssignTicketCommandHandler AssignTicket { get; }

    public Ticket? AddedTicket { get; private set; }

    /// <summary>
    /// Configures a repository to return an existing ticket.
    /// </summary>
    public Ticket ExistingTicket(Ticket ticket)
    {
        Tickets
            .Setup(t => t.GetByIdAsync(
                ticket.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        return ticket;
    }

    /// <summary>
    /// Configures the repository to return an agent.
    /// </summary>
    public void ExistingAgent(
        int id,
        string fullName = "Alex Turner",
        bool isActive = true)
    {
        var agent = new Agent(
            fullName,
            $"agent{id}@example.com",
            maxOpenTickets: 10,
            Clock.UtcNow);

        if (!isActive)
        {
            agent.Deactivate();
        }

        Agents
            .Setup(a => a.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
    }

    public void VerifySaved(Times times) =>
        UnitOfWork.Verify(
            u => u.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            times);
}
