using Microsoft.AspNetCore.Identity;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Navigation property for all accounts owned by this user.
    /// A user can have multiple accounts (bank, cash, credit cards, etc.).
    /// </summary>
    public ICollection<Account> Accounts { get; set; } = new List<Account>();

    /// <summary>
    /// User's personal settings (currency, timezone, etc.).
    /// </summary>
    public UserSettings? Settings { get; set; }
}