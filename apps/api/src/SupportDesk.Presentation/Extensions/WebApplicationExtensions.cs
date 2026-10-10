using Microsoft.EntityFrameworkCore;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>Development convenience only: migrate and seed without wiping existing records.</summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SupportDbContext>()
            .Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync();
        // Separate pass: SupportDbSeeder returns early when tickets exist.
        await scope.ServiceProvider.GetRequiredService<DevelopmentCredentialSeeder>().SeedAsync();
    }
}
