using MoneyBrain.Web.Application.Common.Interfaces;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// A saved filter for quick access
/// </summary>
public class SavedTransactionFilter : IUserOwnedEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FilterJson { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
