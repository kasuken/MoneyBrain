using MoneyBrain.Web.Application.Tips.DTOs;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service for managing educational financial tips.
/// </summary>
public interface IEducationalTipService
{
    /// <summary>
    /// Gets all active educational tips for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active educational tips.</returns>
    Task<List<EducationalTipDto>> GetActiveTipsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets educational tips filtered by category.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="category">The category to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tips in the specified category.</returns>
    Task<List<EducationalTipDto>> GetTipsByCategoryAsync(
        string userId,
        string category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific educational tip by ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="tipId">The tip ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The educational tip, or null if not found.</returns>
    Task<EducationalTipDto?> GetTipByIdAsync(
        string userId,
        int tipId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of unseen educational tips for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of unseen tips.</returns>
    Task<int> GetUnseenTipCountAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
