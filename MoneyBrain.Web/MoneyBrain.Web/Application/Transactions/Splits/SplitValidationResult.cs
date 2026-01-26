namespace MoneyBrain.Web.Application.Transactions.Splits;

/// <summary>
/// Result of split transaction validation
/// </summary>
public class SplitValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    
    public static SplitValidationResult Success() => new() { IsValid = true };
    
    public static SplitValidationResult Failure(params string[] errors) => new() 
    { 
        IsValid = false, 
        Errors = errors.ToList() 
    };
}
