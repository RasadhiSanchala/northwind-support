using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Infrastructure.Data;

/// <summary>Development only; backfills credentials for already-seeded agents as well.</summary>
public sealed class DevelopmentCredentialSeeder(
    SupportDbContext db,
    IConfiguration configuration,
    Pbkdf2PasswordService passwords,
    ILogger<DevelopmentCredentialSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var demoPassword = configuration["DevelopmentAuth:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
            throw new InvalidOperationException("DevelopmentAuth:DemoPassword is required in Development.");

        var agents = await db.Set<Agent>().AsNoTracking().Select(x => x.Id).ToListAsync(ct);
        var alreadySeeded = await db.Set<AgentCredential>().AsNoTracking()
            .Select(x => x.AgentId).ToListAsync(ct);
        var existing = alreadySeeded.ToHashSet();
        var added = 0;
        foreach (var id in agents)
        {
            if (existing.Contains(id)) continue;
            db.Set<AgentCredential>().Add(new AgentCredential(id, passwords.Hash(demoPassword)));
            added++;
        }
        if (added > 0) await db.SaveChangesAsync(ct);
        logger.LogInformation("Created {Count} missing development agent credentials.", added);
    }
}
