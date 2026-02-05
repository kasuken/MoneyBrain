using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Legal;

public class LegalService : ILegalService
{
    private readonly ApplicationDbContext _context;

    public LegalService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<string, LegalDocumentDto>> GetCurrentDocumentsAsync()
    {
        var documents = await _context.LegalDocuments
            .Where(d => d.Type == "Terms" || d.Type == "Privacy")
            .GroupBy(d => d.Type)
            .Select(g => g.OrderByDescending(d => d.EffectiveDate).First())
            .ToListAsync();

        return documents.ToDictionary(
            d => d.Type,
            d => new LegalDocumentDto
            {
                Type = d.Type,
                Version = d.Version,
                EffectiveDate = d.EffectiveDate,
                Content = d.Content
            });
    }

    public async Task RecordAcceptanceAsync(string userId, string documentType, string version, string method)
    {
        var acceptance = new UserLegalAcceptance
        {
            UserId = userId,
            DocumentType = documentType,
            DocumentVersion = version,
            AcceptedAt = DateTime.UtcNow,
            AcceptanceMethod = method
        };

        _context.UserLegalAcceptances.Add(acceptance);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, bool>> CheckAcceptanceStatusAsync(string userId)
    {
        var currentDocuments = await GetCurrentDocumentsAsync();
        var result = new Dictionary<string, bool>();

        foreach (var (docType, doc) in currentDocuments)
        {
            var hasAccepted = await _context.UserLegalAcceptances
                .AnyAsync(a => a.UserId == userId 
                    && a.DocumentType == docType 
                    && a.DocumentVersion == doc.Version);

            result[docType] = hasAccepted;
        }

        return result;
    }

    public async Task<List<UserLegalAcceptanceDto>> GetUserAcceptanceHistoryAsync(string userId)
    {
        return await _context.UserLegalAcceptances
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.AcceptedAt)
            .Select(a => new UserLegalAcceptanceDto
            {
                DocumentType = a.DocumentType,
                DocumentVersion = a.DocumentVersion,
                AcceptedAt = a.AcceptedAt,
                AcceptanceMethod = a.AcceptanceMethod
            })
            .ToListAsync();
    }
}
