using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Services;

public sealed record AuthenticatedAgent(int Id, string FullName, string Email);

/// <summary>Validates agent credentials and active status without revealing which check failed.</summary>
public sealed class AgentAuthenticationService(
    SupportDbContext db, Pbkdf2PasswordService passwords)
{
    public async Task<AuthenticatedAgent?> AuthenticateAsync(
        string? email, string? password, CancellationToken ct)
    {
        var normalized = email?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized) || string.IsNullOrEmpty(password)) return null;

        var candidate = await (
            from agent in db.Set<Agent>().AsNoTracking()
            join credential in db.Set<AgentCredential>().AsNoTracking()
                on agent.Id equals credential.AgentId
            where agent.Email == normalized
            select new { agent.Id, agent.FullName, agent.Email,
                agent.IsActive, credential.PasswordHash }
        ).SingleOrDefaultAsync(ct);

        if (candidate is null || !candidate.IsActive ||
            !passwords.Verify(password, candidate.PasswordHash)) return null;

        return new AuthenticatedAgent(candidate.Id, candidate.FullName, candidate.Email);
    }

    public async Task<AuthenticatedAgent?> GetActiveAgentAsync(int agentId, CancellationToken ct) =>
        await db.Set<Agent>().AsNoTracking()
            .Where(a => a.Id == agentId && a.IsActive)
            .Select(a => new AuthenticatedAgent(a.Id, a.FullName, a.Email))
            .FirstOrDefaultAsync(ct);
}
