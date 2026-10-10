
using SupportDesk.Application.Services;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.UnitTests.Application.Services;

public class AutomaticAgentSelectorTests
{
    private readonly AutomaticAgentSelector _selector = new();

    private static AgentAssignmentCandidate Agent(
        int id,
        int openTickets,
        int capacity = 5,
        bool active = true,
        bool specialist = false) =>
        new(
            AgentId: id,
            IsActive: active,
            MaxOpenTickets: capacity,
            OpenTicketCount: openTickets,
            IsSpecialist: specialist);

    [Fact]
    public void NormalCategory_SelectsLeastLoadedAgent()
    {
        var candidates = new[]
        {
            Agent(1, openTickets: 4),
            Agent(2, openTickets: 1),
            Agent(3, openTickets: 2, specialist: true)
        };

        var result = _selector.Select(
            candidates,
            requiresSpecialist: false);

        Assert.Equal(2, result.AgentId);
        Assert.Null(result.UnassignedReason);
    }

    [Fact]
    public void SpecialistCategory_OnlyConsidersQualifiedAgents()
    {
        var candidates = new[]
        {
            Agent(1, openTickets: 0, specialist: false),
            Agent(3, openTickets: 2, specialist: true),
            Agent(5, openTickets: 1, specialist: true)
        };

        var result = _selector.Select(
            candidates,
            requiresSpecialist: true);

        Assert.Equal(5, result.AgentId);
    }

    [Fact]
    public void InactiveAgents_AreExcluded()
    {
        var candidates = new[]
        {
            Agent(1, openTickets: 0, active: false),
            Agent(2, openTickets: 2, active: true)
        };

        var result = _selector.Select(
            candidates,
            requiresSpecialist: false);

        Assert.Equal(2, result.AgentId);
    }

    [Fact]
    public void AgentsAtFullCapacity_AreExcluded()
    {
        var candidates = new[]
        {
            Agent(1, openTickets: 5, capacity: 5),
            Agent(2, openTickets: 3, capacity: 5)
        };

        var result = _selector.Select(
            candidates,
            requiresSpecialist: false);

        Assert.Equal(2, result.AgentId);
    }

    [Fact]
    public void EqualWorkload_SelectsLowestAgentId()
    {
        var candidates = new[]
        {
            Agent(7, openTickets: 2),
            Agent(2, openTickets: 2),
            Agent(5, openTickets: 2)
        };

        var result = _selector.Select(
            candidates,
            requiresSpecialist: false);

        Assert.Equal(2, result.AgentId);
    }

    [Fact]
    public void NoEligibleSpecialist_ReturnsUnassignedReason()
    {
        var candidates = new[]
        {
            Agent(1, openTickets: 0, specialist: false),
            Agent(3, openTickets: 5, capacity: 5, specialist: true),
            Agent(5, openTickets: 0, active: false, specialist: true)
        };

        var result = _selector.Select(
            candidates,
            requiresSpecialist: true);

        Assert.Null(result.AgentId);
        Assert.NotNull(result.UnassignedReason);
        Assert.Contains(
            "specialist",
            result.UnassignedReason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllAgentsAtCapacity_ReturnsUnassignedReason()
    {
        var candidates = new[]
        {
            Agent(1, openTickets: 5, capacity: 5),
            Agent(2, openTickets: 8, capacity: 8)
        };

        var result = _selector.Select(
            candidates,
            requiresSpecialist: false);

        Assert.Null(result.AgentId);
        Assert.False(
            string.IsNullOrWhiteSpace(result.UnassignedReason));
    }

    [Fact]
    public void NoAgents_ReturnsUnassignedReason()
    {
        var result = _selector.Select(
            Array.Empty<AgentAssignmentCandidate>(),
            requiresSpecialist: false);

        Assert.Null(result.AgentId);
        Assert.NotNull(result.UnassignedReason);
    }
}
