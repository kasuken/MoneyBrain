namespace MoneyBrain.Web.Application.Legal;

public interface ILegalService
{
    Task<Dictionary<string, LegalDocumentDto>> GetCurrentDocumentsAsync();
    Task RecordAcceptanceAsync(string userId, string documentType, string version, string method);
    Task<Dictionary<string, bool>> CheckAcceptanceStatusAsync(string userId);
    Task<List<UserLegalAcceptanceDto>> GetUserAcceptanceHistoryAsync(string userId);
}

public class LegalDocumentDto
{
    public string Type { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class UserLegalAcceptanceDto
{
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentVersion { get; set; } = string.Empty;
    public DateTime AcceptedAt { get; set; }
    public string AcceptanceMethod { get; set; } = string.Empty;
}
