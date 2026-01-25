using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    /// <summary>
    /// Accounts represent asset and liability accounts (bank, cash, credit cards, loans).
    /// </summary>
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Account entity
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);

            // Relationship: Account belongs to one ApplicationUser
            entity.HasOne(a => a.User)
                .WithMany(u => u.Accounts)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => new { a.UserId, a.IsActive });
            entity.HasIndex(a => new { a.UserId, a.Type });

            // Decimal precision for money values
            entity.Property(a => a.OpeningBalance)
                .HasPrecision(18, 2);
        });
    }
}