using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Common;

/// <summary>
/// Extension methods for <see cref="IQueryable{T}"/> used across the Application layer.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Filters a queryable to only include entities that belong to the specified user.
    /// EF Core translates this correctly because the entity implements <see cref="IUserOwnedEntity"/>
    /// as a direct property, not through an interface shadow column.
    /// </summary>
    /// <typeparam name="T">An entity type that implements <see cref="IUserOwnedEntity"/>.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="userId">The user identifier to filter by.</param>
    /// <returns>A queryable restricted to the specified user's entities.</returns>
    public static IQueryable<T> ForUser<T>(this IQueryable<T> query, string userId)
        where T : class, IUserOwnedEntity
        => query.Where(e => e.UserId == userId);

    /// <summary>
    /// Eagerly loads the standard navigation properties used by most transaction queries:
    /// <see cref="Transaction.Account"/>, <see cref="Transaction.Payee"/>,
    /// <see cref="Transaction.Category"/> (with <see cref="Category.CategoryGroup"/>).
    /// </summary>
    /// <param name="query">The source transaction queryable.</param>
    /// <returns>The queryable with Includes applied.</returns>
    public static IQueryable<Transaction> IncludeTransactionDetails(this IQueryable<Transaction> query)
        => query
            .Include(t => t.Account)
            .Include(t => t.Payee)
            .Include(t => t.Category)
                .ThenInclude(c => c!.CategoryGroup);
}
