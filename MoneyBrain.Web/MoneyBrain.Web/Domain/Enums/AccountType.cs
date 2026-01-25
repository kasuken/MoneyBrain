namespace MoneyBrain.Web.Domain.Enums;

/// <summary>
/// Represents the type of account in the personal finance system.
/// </summary>
public enum AccountType
{
    /// <summary>
    /// Asset accounts represent value owned (bank, cash, savings, investments).
    /// </summary>
    Asset = 1,

    /// <summary>
    /// Liability accounts represent money owed (credit cards, loans, mortgages).
    /// </summary>
    Liability = 2
}
