namespace MoneyBrain.Web.Application.Common.Interfaces;

/// <summary>
/// Marks a domain entity as owned by a specific user.
/// Enables generic, type-safe user-scoping of EF Core queries via <see cref="QueryableExtensions"/>.
/// </summary>
public interface IUserOwnedEntity
{
    /// <summary>
    /// The identifier of the user who owns this entity.
    /// </summary>
    string UserId { get; }
}
