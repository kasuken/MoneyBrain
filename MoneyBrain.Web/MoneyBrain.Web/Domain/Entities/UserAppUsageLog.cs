using System.ComponentModel.DataAnnotations;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Track app usage patterns for behavioral insights.
/// </summary>
public class UserAppUsageLog : IUserOwnedEntity
{
    /// <summary>
    /// Unique identifier for the usage log entry.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The user this activity belongs to.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public required string UserId { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Type of activity (e.g., "TransactionCreated", "BudgetViewed", "ReportGenerated").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string ActivityType { get; set; }

    /// <summary>
    /// Optional reference to a specific entity (transaction ID, budget ID, etc.).
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// When the activity occurred.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// When the log entry was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
