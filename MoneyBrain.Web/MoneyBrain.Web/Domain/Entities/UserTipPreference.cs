using System.ComponentModel.DataAnnotations;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Track individual tip dismissals and user preferences for educational tips.
/// </summary>
public class UserTipPreference : IUserOwnedEntity
{
    /// <summary>
    /// Unique identifier for the preference record.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// The user this preference belongs to.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public required string UserId { get; init; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// The educational tip this preference is for.
    /// </summary>
    [Required]
    public int EducationalTipId { get; set; }

    /// <summary>
    /// Navigation property to the educational tip.
    /// </summary>
    public EducationalTip? EducationalTip { get; set; }

    /// <summary>
    /// Whether this tip is enabled for the user.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the user has dismissed this tip.
    /// </summary>
    public bool IsDismissed { get; set; } = false;

    /// <summary>
    /// When the tip was dismissed (if applicable).
    /// </summary>
    public DateTime? DismissedAt { get; set; }

    /// <summary>
    /// When the preference was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
