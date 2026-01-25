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

    /// <summary>
    /// Opening balance adjustments provide audit trail for account opening balance changes.
    /// </summary>
    public DbSet<OpeningBalanceAdjustment> OpeningBalanceAdjustments => Set<OpeningBalanceAdjustment>();

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

        // Configure OpeningBalanceAdjustment entity
        modelBuilder.Entity<OpeningBalanceAdjustment>(entity =>
        {
            entity.HasKey(oba => oba.Id);

            // Relationship: Adjustment belongs to one Account
            entity.HasOne(oba => oba.Account)
                .WithMany(a => a.OpeningBalanceAdjustments)
                .HasForeignKey(oba => oba.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(oba => oba.AccountId);
            entity.HasIndex(oba => oba.AdjustedAt);
            entity.HasIndex(oba => new { oba.AccountId, oba.AdjustedAt });

            // Decimal precision for money values
            entity.Property(oba => oba.PreviousBalance).HasPrecision(18, 2);
            entity.Property(oba => oba.NewBalance).HasPrecision(18, 2);
            entity.Property(oba => oba.AdjustmentAmount).HasPrecision(18, 2);
        });
    }
}