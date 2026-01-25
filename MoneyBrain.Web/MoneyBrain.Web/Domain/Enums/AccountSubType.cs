namespace MoneyBrain.Web.Domain.Enums;

/// <summary>
/// Specific sub-types for accounts to help with organization and reporting.
/// </summary>
public enum AccountSubType
{
    // Asset sub-types
    Cash = 1,
    Checking = 2,
    Savings = 3,
    Investment = 4,
    OtherAsset = 99,

    // Liability sub-types
    CreditCard = 101,
    Loan = 102,
    Mortgage = 103,
    OtherLiability = 199
}
