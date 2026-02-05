using System.ComponentModel.DataAnnotations;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Domain.Entities;

public class UserLegalAcceptance
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string DocumentType { get; set; } = string.Empty; // "Terms", "Privacy"
    
    [Required]
    [MaxLength(20)]
    public string DocumentVersion { get; set; } = string.Empty;
    
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    [MaxLength(50)]
    public string AcceptanceMethod { get; set; } = string.Empty; // "signup", "re-acceptance"
    
    public ApplicationUser? User { get; set; }
}
