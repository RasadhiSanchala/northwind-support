
using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Access to Customer aggregates.
/// </summary>
public interface ICustomerRepository
{
    Task<bool> ExistsAsync(int id, CancellationToken ct);

    /// <summary>
    /// Retrieves the customer, including their membership tier.
    /// </summary>
    Task<Customer?> GetByIdAsync(int id, CancellationToken ct);
}
