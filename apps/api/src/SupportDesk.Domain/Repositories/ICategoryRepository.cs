
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Access to Category aggregates.
/// </summary>
public interface ICategoryRepository
{
    Task<bool> ExistsAsync(int id, CancellationToken ct);

    /// <summary>
    /// Retrieves category metadata, including priority and
    /// specialization rules.
    /// </summary>
    Task<Category?> GetByIdAsync(int id, CancellationToken ct);
}
