using System.ComponentModel.DataAnnotations;

namespace MoneyBrain.Web.Domain.Entities;

public class LegalDocument
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // "Terms", "Privacy"
    
    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = string.Empty; // e.g., "1.0"
    
    public DateTime EffectiveDate { get; set; }
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public bool IsMaterialChange { get; set; }
    
    public DateTime CreatedAt { get; set; } = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
