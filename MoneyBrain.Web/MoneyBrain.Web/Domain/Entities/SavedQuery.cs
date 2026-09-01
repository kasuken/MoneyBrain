using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// A saved query for the Insight Explorer feature
/// </summary>
public class SavedQuery : IUserOwnedEntity
{
    public int Id { get; init; }

    public required string UserId { get; init; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// JSON-serialized QueryDefinition
    /// </summary>
    public required string QueryDefinitionJson { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ApplicationUser? User { get; set; }
}
