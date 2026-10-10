
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Infrastructure;

/// <summary>
/// Registers database repositories and infrastructure services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. " +
                "See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp =>
            sp.GetRequiredService<SupportDbContext>());

        // Application clock
        services.AddSingleton<IClock, SystemClock>();

        // Repositories
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        // Read queries
        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();

        // Load SLA rules from configuration.
        // No additional configuration binder package is required.
        services.AddSingleton<SlaOptions>(_ => new SlaOptions
        {
            BaseWindowHours = new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["Low"] = ReadPositiveDouble(
                    configuration,
                    "Sla:BaseWindowHours:Low"),

                ["Medium"] = ReadPositiveDouble(
                    configuration,
                    "Sla:BaseWindowHours:Medium"),

                ["High"] = ReadPositiveDouble(
                    configuration,
                    "Sla:BaseWindowHours:High"),

                ["Critical"] = ReadPositiveDouble(
                    configuration,
                    "Sla:BaseWindowHours:Critical")
            },

            PremiumCustomerMultiplier = ReadPositiveDouble(
                configuration,
                "Sla:PremiumCustomerMultiplier"),

            MinimumWindowHours = ReadPositiveDouble(
                configuration,
                "Sla:MinimumWindowHours"),

            AtRiskThresholdPercent = ReadPositiveDouble(
                configuration,
                "Sla:AtRiskThresholdPercent")
        });

        // Shared calculator used when creating and escalating tickets.
        services.AddSingleton<SlaDeadlineCalculator>();

        // Development database seeder
        services.AddScoped<SupportDbSeeder>();

        return services;
    }

    /// <summary>
    /// Reads a positive numeric configuration value.
    /// Fails clearly if a required SLA setting is missing
    /// or invalid.
    /// </summary>
    private static double ReadPositiveDouble(
        IConfiguration configuration,
        string key)
    {
        var rawValue = configuration[key];

        if (!double.TryParse(
                rawValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value) ||
            !double.IsFinite(value) ||
            value <= 0)
        {
            throw new InvalidOperationException(
                $"SLA configuration '{key}' must be " +
                "a positive finite number.");
        }

        return value;
    }
}
