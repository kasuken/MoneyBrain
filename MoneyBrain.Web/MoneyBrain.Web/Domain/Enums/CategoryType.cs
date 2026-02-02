namespace MoneyBrain.Web.Domain.Enums;

/// <summary>
/// Defines whether a category group represents income or expenses
/// </summary>
public enum CategoryType
{
    /// <summary>
    /// Expense categories - amounts are typically negative
    /// </summary>
    Expense = 0,

    /// <summary>
    /// Income categories - amounts are typically positive
    /// </summary>
    Income = 1
}
