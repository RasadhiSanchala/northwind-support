
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Features.Agents.Queries.GetAgents;
using SupportDesk.Application.Features.Categories.Queries.GetCategories;
using SupportDesk.Application.Features.Customers.Queries.GetCustomer;
using SupportDesk.Application.Features.Customers.Queries.GetCustomers;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.SearchTickets;
using SupportDesk.Application.Services;

namespace SupportDesk.Application;

/// <summary>
/// Registers application command handlers,
/// query handlers, services and validators.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        // Ticket commands.
        services.AddScoped<RaiseTicketCommandHandler>();
        services.AddScoped<ChangeTicketStatusCommandHandler>();
        services.AddScoped<AssignTicketCommandHandler>();

        // Ticket queries.
        services.AddScoped<SearchTicketsQueryHandler>();
        services.AddScoped<GetTicketQueryHandler>();

        // Customer queries.
        services.AddScoped<GetCustomersQueryHandler>();
        services.AddScoped<GetCustomerQueryHandler>();

        // Agent queries.
        services.AddScoped<GetAgentsQueryHandler>();

        // Category queries.
        services.AddScoped<GetCategoriesQueryHandler>();

        // Stateless agent selection service.
        services.AddSingleton<AutomaticAgentSelector>();

        // Request validation.
        services.AddValidatorsFromAssemblyContaining<
            RaiseTicketCommandHandler>(
                ServiceLifetime.Singleton);

        return services;
    }
}
