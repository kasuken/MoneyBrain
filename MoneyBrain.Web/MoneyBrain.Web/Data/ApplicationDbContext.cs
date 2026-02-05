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

    /// <summary>
    /// Account balance snapshots track point-in-time balance values for history and reporting.
    /// </summary>
    public DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots => Set<AccountBalanceSnapshot>();

    /// <summary>
    /// Manual balance adjustments track explicit balance changes with full audit trail.
    /// </summary>
    public DbSet<ManualBalanceAdjustment> ManualBalanceAdjustments => Set<ManualBalanceAdjustment>();

    /// <summary>
    /// User settings store personal preferences like currency and timezone.
    /// </summary>
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    /// <summary>
    /// Category groups for organizing categories
    /// </summary>
    public DbSet<CategoryGroup> CategoryGroups => Set<CategoryGroup>();

    /// <summary>
    /// Categories for transactions - each belongs to exactly one group
    /// </summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>
    /// Payees (vendors, people, etc.) for transactions
    /// </summary>
    public DbSet<Payee> Payees => Set<Payee>();

    /// <summary>
    /// Transactions - each belongs to exactly one account
    /// </summary>
    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>
    /// Transaction splits - sum must equal transaction amount
    /// </summary>
    public DbSet<TransactionSplit> TransactionSplits => Set<TransactionSplit>();

    /// <summary>
    /// Saved transaction filters for quick access
    /// </summary>
    public DbSet<SavedTransactionFilter> SavedTransactionFilters => Set<SavedTransactionFilter>();

    /// <summary>
    /// Monthly budgets (envelopes) for categories - planned amounts per category per month
    /// </summary>
    public DbSet<MonthlyBudget> MonthlyBudgets => Set<MonthlyBudget>();

    /// <summary>
    /// Named budgets containing multiple category allocations for a specific period
    /// </summary>
    public DbSet<Budget> Budgets => Set<Budget>();

    /// <summary>
    /// Budget category allocations - links budgets to categories with planned amounts
    /// </summary>
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();

    /// <summary>
    /// Account reconciliations track statement matching sessions
    /// </summary>
    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();

    /// <summary>
    /// Ledger entries for double-entry bookkeeping - every transaction generates at least 2 entries
    /// </summary>
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    /// <summary>
    /// User licenses track subscription/license status with Stripe integration
    /// </summary>
    public DbSet<UserLicense> UserLicenses => Set<UserLicense>();

    /// <summary>
    /// Saved queries for the Insight Explorer feature
    /// </summary>
    public DbSet<SavedQuery> SavedQueries => Set<SavedQuery>();

    /// <summary>
    /// Legal documents (Terms, Privacy) with versioning
    /// </summary>
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();

    /// <summary>
    /// User legal acceptance records for Terms and Privacy
    /// </summary>
    public DbSet<UserLegalAcceptance> UserLegalAcceptances => Set<UserLegalAcceptance>();

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

            // Relationship: Credit card can link to payment account
            entity.HasOne(a => a.LinkedPaymentAccount)
                .WithMany()
                .HasForeignKey(a => a.LinkedPaymentAccountId)
                .OnDelete(DeleteBehavior.NoAction);

            // Indexes for common queries
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => new { a.UserId, a.IsActive });
            entity.HasIndex(a => new { a.UserId, a.Type });
            entity.HasIndex(a => a.BillingCycleDay);

            // Decimal precision for money values
            entity.Property(a => a.OpeningBalance)
                .HasPrecision(18, 2);

            entity.Property(a => a.MonthlySpendingLimit)
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

        // Configure AccountBalanceSnapshot entity
        modelBuilder.Entity<AccountBalanceSnapshot>(entity =>
        {
            entity.HasKey(abs => abs.Id);

            // Relationship: Snapshot belongs to one Account
            entity.HasOne(abs => abs.Account)
                .WithMany(a => a.BalanceSnapshots)
                .HasForeignKey(abs => abs.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(abs => abs.AccountId);
            entity.HasIndex(abs => abs.SnapshotDate);
            entity.HasIndex(abs => new { abs.AccountId, abs.SnapshotDate });
            entity.HasIndex(abs => new { abs.AccountId, abs.Type });

            // Decimal precision for money values
            entity.Property(abs => abs.Balance).HasPrecision(18, 2);
        });

        // Configure ManualBalanceAdjustment entity
        modelBuilder.Entity<ManualBalanceAdjustment>(entity =>
        {
            entity.HasKey(mba => mba.Id);

            // Relationship: Adjustment belongs to one Account
            entity.HasOne(mba => mba.Account)
                .WithMany(a => a.ManualBalanceAdjustments)
                .HasForeignKey(mba => mba.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(mba => mba.AccountId);
            entity.HasIndex(mba => mba.AdjustmentDate);
            entity.HasIndex(mba => new { mba.AccountId, mba.AdjustmentDate });
            entity.HasIndex(mba => new { mba.AccountId, mba.IsReconciled });
            entity.HasIndex(mba => mba.Category);

            // Decimal precision for money values
            entity.Property(mba => mba.Amount).HasPrecision(18, 2);
        });

        // Configure UserSettings entity
        modelBuilder.Entity<UserSettings>(entity =>
        {
            entity.HasKey(us => us.Id);

            // Relationship: One user has one settings record
            // Make the relationship optional to avoid FK constraint issues during setup
            entity.HasOne(us => us.User)
                .WithOne(u => u.Settings)
                .HasForeignKey<UserSettings>(us => us.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // Unique index on UserId (one settings per user)
            entity.HasIndex(us => us.UserId).IsUnique();
        });

        // Configure CategoryGroup entity
        modelBuilder.Entity<CategoryGroup>(entity =>
        {
            entity.HasKey(cg => cg.Id);

            entity.HasIndex(cg => cg.UserId);
            entity.HasIndex(cg => new { cg.UserId, cg.IsActive });
        });

        // Configure Category entity
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);

            // Relationship: Category belongs to one CategoryGroup
            entity.HasOne(c => c.CategoryGroup)
                .WithMany(cg => cg.Categories)
                .HasForeignKey(c => c.CategoryGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(c => c.UserId);
            entity.HasIndex(c => c.CategoryGroupId);
            entity.HasIndex(c => new { c.UserId, c.IsActive });
        });

        // Configure Payee entity
        modelBuilder.Entity<Payee>(entity =>
        {
            entity.HasKey(p => p.Id);

            // Relationship: Payee can have a default Category
            entity.HasOne(p => p.DefaultCategory)
                .WithMany()
                .HasForeignKey(p => p.DefaultCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => p.UserId);
            entity.HasIndex(p => new { p.UserId, p.IsActive });
        });

        // Configure Transaction entity
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(t => t.Id);

            // Relationship: Transaction belongs to one Account
            entity.HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: Transaction can have a Payee
            entity.HasOne(t => t.Payee)
                .WithMany(p => p.Transactions)
                .HasForeignKey(t => t.PayeeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Relationship: Transaction can have a Category
            entity.HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // Relationship: Transaction can link to another transaction (transfer)
            entity.HasOne(t => t.TransferTransaction)
                .WithMany()
                .HasForeignKey(t => t.TransferTransactionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Relationship: Credit card bill links to source credit card account
            entity.HasOne(t => t.CreditCardBillingSourceAccount)
                .WithMany()
                .HasForeignKey(t => t.CreditCardBillingSourceAccountId)
                .OnDelete(DeleteBehavior.NoAction);

            // Indexes for common queries
            entity.HasIndex(t => t.UserId);
            entity.HasIndex(t => t.AccountId);
            entity.HasIndex(t => t.Date);
            entity.HasIndex(t => new { t.UserId, t.Date });
            entity.HasIndex(t => new { t.AccountId, t.Date });
            entity.HasIndex(t => new { t.UserId, t.Status });
            entity.HasIndex(t => new { t.UserId, t.IsReconciled });
            entity.HasIndex(t => t.PayeeId);
            entity.HasIndex(t => t.CategoryId);
            entity.HasIndex(t => t.CreditCardBillingSourceAccountId);

            // Decimal precision for money values
            entity.Property(t => t.Amount).HasPrecision(18, 2);
        });

        // Configure TransactionSplit entity
        modelBuilder.Entity<TransactionSplit>(entity =>
        {
            entity.HasKey(ts => ts.Id);

            // Relationship: Split belongs to one Transaction
            entity.HasOne(ts => ts.Transaction)
                .WithMany(t => t.Splits)
                .HasForeignKey(ts => ts.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: Split can have a Category
            entity.HasOne(ts => ts.Category)
                .WithMany()
                .HasForeignKey(ts => ts.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(ts => ts.TransactionId);
            entity.HasIndex(ts => ts.CategoryId);

            // Decimal precision for money values
            entity.Property(ts => ts.Amount).HasPrecision(18, 2);
        });

        // Configure SavedTransactionFilter entity
        modelBuilder.Entity<SavedTransactionFilter>(entity =>
        {
            entity.HasKey(stf => stf.Id);

            entity.HasIndex(stf => stf.UserId);
            entity.HasIndex(stf => new { stf.UserId, stf.IsDefault });

            entity.Property(stf => stf.Name).IsRequired().HasMaxLength(100);
            entity.Property(stf => stf.FilterJson).IsRequired();
        });

        // Configure MonthlyBudget entity
        modelBuilder.Entity<MonthlyBudget>(entity =>
        {
            entity.HasKey(mb => mb.Id);

            // Relationship: Budget belongs to one Category
            entity.HasOne(mb => mb.Category)
                .WithMany()
                .HasForeignKey(mb => mb.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(mb => mb.UserId);
            entity.HasIndex(mb => mb.CategoryId);
            entity.HasIndex(mb => new { mb.CategoryId, mb.IsDefault });
            entity.HasIndex(mb => new { mb.UserId, mb.Year, mb.Month });
            entity.HasIndex(mb => new { mb.CategoryId, mb.Year, mb.Month });

            // Unique constraint: one default budget per category
            entity.HasIndex(mb => new { mb.CategoryId, mb.IsDefault })
                .IsUnique()
                .HasFilter("[IsDefault] = 1");

            // Unique constraint: one budget per category per month (for non-default budgets)
            entity.HasIndex(mb => new { mb.CategoryId, mb.Year, mb.Month })
                .IsUnique()
                .HasFilter("[IsDefault] = 0");

            // Decimal precision for money values
            entity.Property(mb => mb.PlannedAmount).HasPrecision(18, 2);
        });

        // Configure Budget entity
        modelBuilder.Entity<Budget>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.Name).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Description).HasMaxLength(500);
            entity.Property(b => b.UserId).IsRequired();

            // Indexes for common queries
            entity.HasIndex(b => b.UserId);
            entity.HasIndex(b => new { b.UserId, b.Year, b.Month });
            entity.HasIndex(b => new { b.UserId, b.Name });
            
            // Unique constraint: one default budget with a given name per user
            entity.HasIndex(b => new { b.UserId, b.Name, b.IsDefault })
                .IsUnique()
                .HasFilter("[IsDefault] = 1");
            
            // Unique constraint: one budget with a given name per period (for non-defaults)
            entity.HasIndex(b => new { b.UserId, b.Name, b.Year, b.Month })
                .IsUnique()
                .HasFilter("[IsDefault] = 0");

            // Relationship: Budget has many BudgetCategories
            entity.HasMany(b => b.BudgetCategories)
                .WithOne(bc => bc.Budget)
                .HasForeignKey(bc => bc.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BudgetCategory entity
        modelBuilder.Entity<BudgetCategory>(entity =>
        {
            entity.HasKey(bc => bc.Id);

            entity.Property(bc => bc.PlannedAmount).HasPrecision(18, 2);

            // Unique constraint: one entry per budget per category
            entity.HasIndex(bc => new { bc.BudgetId, bc.CategoryId }).IsUnique();

            // Relationship: BudgetCategory belongs to one Budget
            entity.HasOne(bc => bc.Budget)
                .WithMany(b => b.BudgetCategories)
                .HasForeignKey(bc => bc.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: BudgetCategory references one Category
            entity.HasOne(bc => bc.Category)
                .WithMany()
                .HasForeignKey(bc => bc.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure LedgerEntry entity
        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.HasKey(le => le.Id);

            // Relationship: LedgerEntry belongs to one Transaction
            entity.HasOne(le => le.Transaction)
                .WithMany(t => t.LedgerEntries)
                .HasForeignKey(le => le.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: LedgerEntry affects one Account
            entity.HasOne(le => le.Account)
                .WithMany()
                .HasForeignKey(le => le.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship: LedgerEntry can be associated with a Category
            entity.HasOne(le => le.Category)
                .WithMany()
                .HasForeignKey(le => le.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for common queries
            entity.HasIndex(le => le.UserId);
            entity.HasIndex(le => le.TransactionId);
            entity.HasIndex(le => le.AccountId);
            entity.HasIndex(le => le.EntryDate);
            entity.HasIndex(le => new { le.AccountId, le.EntryDate });
            entity.HasIndex(le => new { le.UserId, le.EntryDate });

            // Decimal precision for money values
            entity.Property(le => le.DebitAmount).HasPrecision(18, 2);
            entity.Property(le => le.CreditAmount).HasPrecision(18, 2);
        });

        // Configure Reconciliation entity
        modelBuilder.Entity<Reconciliation>(entity =>
        {
            entity.HasKey(r => r.Id);

            // Decimal precision for money values
            entity.Property(r => r.OpeningBalance).HasPrecision(18, 2);
            entity.Property(r => r.StatementBalance).HasPrecision(18, 2);
            entity.Property(r => r.ReconciledBalance).HasPrecision(18, 2);
            entity.Property(r => r.Difference).HasPrecision(18, 2);
        });

        // Configure SavedQuery entity
        modelBuilder.Entity<SavedQuery>(entity =>
        {
            entity.HasKey(sq => sq.Id);

            entity.HasIndex(sq => sq.UserId);
            entity.HasIndex(sq => new { sq.UserId, sq.IsDefault });

            entity.Property(sq => sq.Name).IsRequired().HasMaxLength(200);
            entity.Property(sq => sq.Description).HasMaxLength(500);
            entity.Property(sq => sq.QueryDefinitionJson).IsRequired();

            // Relationship: SavedQuery belongs to one ApplicationUser
            entity.HasOne(sq => sq.User)
                .WithMany()
                .HasForeignKey(sq => sq.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure LegalDocument entity
        modelBuilder.Entity<LegalDocument>(entity =>
        {
            entity.HasKey(ld => ld.Id);

            entity.HasIndex(ld => new { ld.Type, ld.Version });
            entity.HasIndex(ld => ld.EffectiveDate);

            entity.Property(ld => ld.Type).IsRequired().HasMaxLength(50);
            entity.Property(ld => ld.Version).IsRequired().HasMaxLength(20);
            entity.Property(ld => ld.Content).IsRequired();
        });

        // Configure UserLegalAcceptance entity
        modelBuilder.Entity<UserLegalAcceptance>(entity =>
        {
            entity.HasKey(ula => ula.Id);

            entity.HasIndex(ula => ula.UserId);
            entity.HasIndex(ula => new { ula.UserId, ula.DocumentType });
            entity.HasIndex(ula => ula.AcceptedAt);

            entity.Property(ula => ula.UserId).IsRequired();
            entity.Property(ula => ula.DocumentType).IsRequired().HasMaxLength(50);
            entity.Property(ula => ula.DocumentVersion).IsRequired().HasMaxLength(20);
            entity.Property(ula => ula.AcceptanceMethod).IsRequired().HasMaxLength(50);

            // Relationship: UserLegalAcceptance belongs to one ApplicationUser
            entity.HasOne(ula => ula.User)
                .WithMany()
                .HasForeignKey(ula => ula.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed initial legal documents
        modelBuilder.Entity<LegalDocument>().HasData(
            new LegalDocument
            {
                Id = 1,
                Type = "Terms",
                Version = "1.0",
                EffectiveDate = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),
                IsMaterialChange = false,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Content = @"# MoneyBrain Terms of Service

**Effective Date: February 5, 2026**
**Version: 1.0**

## 1. Acceptance of Terms

By accessing and using MoneyBrain (""the Service""), you accept and agree to be bound by the terms and provisions of this agreement. If you do not agree to these Terms of Service, please do not use the Service.

## 2. Description of Service

MoneyBrain is a personal finance management application that helps users track transactions, manage budgets, and gain insights into their financial data. The Service is provided as a self-hosted or cloud-hosted solution.

## 3. User Accounts and Registration

3.1. You must create an account to use MoneyBrain. You are responsible for maintaining the confidentiality of your account credentials.

3.2. You agree to provide accurate, current, and complete information during registration and to update such information to keep it accurate, current, and complete.

3.3. You are solely responsible for all activities that occur under your account.

## 4. Acceptable Use

4.1. You agree to use MoneyBrain only for lawful purposes and in accordance with these Terms.

4.2. You agree not to:
   - Use the Service in any way that violates any applicable law or regulation
   - Attempt to gain unauthorized access to any portion of the Service
   - Interfere with or disrupt the Service or servers or networks connected to the Service
   - Use the Service to transmit any harmful code, viruses, or malicious software
   - Reverse engineer, decompile, or disassemble the Service (except where permitted by law)

## 5. Data and Privacy

5.1. Your use of the Service is also governed by our Privacy Policy, which is incorporated into these Terms by reference.

5.2. You retain all ownership rights to the financial data you input into MoneyBrain.

5.3. MoneyBrain processes your data solely to provide the Service to you.

## 6. Intellectual Property

6.1. MoneyBrain and its original content, features, and functionality are owned by MoneyBrain and are protected by international copyright, trademark, and other intellectual property laws.

6.2. Our trademarks and trade dress may not be used without our prior written permission.

## 7. Third-Party Services

MoneyBrain may integrate with third-party services (e.g., payment processors). Your use of such third-party services is subject to their respective terms and conditions.

## 8. Subscription and Payment

8.1. Certain features of MoneyBrain may require a paid subscription.

8.2. Subscription fees are billed in advance on a recurring basis (monthly or annually).

8.3. You authorize us to charge the payment method on file for all subscription fees.

8.4. We reserve the right to change subscription fees with at least 30 days' notice.

## 9. Cancellation and Refunds

9.1. You may cancel your subscription at any time through your account settings.

9.2. Upon cancellation, you will continue to have access until the end of your current billing period.

9.3. Refunds are provided at our sole discretion and only in accordance with our refund policy.

## 10. Disclaimer of Warranties

10.1. THE SERVICE IS PROVIDED ""AS IS"" AND ""AS AVAILABLE"" WITHOUT WARRANTIES OF ANY KIND, EITHER EXPRESS OR IMPLIED.

10.2. MoneyBrain does not warrant that the Service will be uninterrupted, secure, or error-free.

10.3. MoneyBrain is not a financial advisor. The Service provides tools for financial tracking and analysis but does not constitute financial, tax, or legal advice.

## 11. Limitation of Liability

11.1. TO THE MAXIMUM EXTENT PERMITTED BY LAW, MONEYBRAIN SHALL NOT BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES, OR ANY LOSS OF PROFITS OR REVENUES.

11.2. IN NO EVENT SHALL MONEYBRAIN'S TOTAL LIABILITY EXCEED THE AMOUNT YOU PAID TO MONEYBRAIN IN THE TWELVE (12) MONTHS PRECEDING THE CLAIM.

## 12. Indemnification

You agree to indemnify and hold MoneyBrain harmless from any claims, damages, losses, liabilities, and expenses (including attorneys' fees) arising out of your use of the Service or violation of these Terms.

## 13. Modifications to Terms

13.1. We reserve the right to modify these Terms at any time.

13.2. If we make material changes, we will notify you by email or through the Service.

13.3. Your continued use of the Service after such modifications constitutes acceptance of the updated Terms.

## 14. Termination

14.1. We may terminate or suspend your account and access to the Service immediately, without prior notice, for any breach of these Terms.

14.2. Upon termination, your right to use the Service will immediately cease.

14.3. You may terminate your account at any time by contacting us or through your account settings.

## 15. Governing Law

These Terms shall be governed by and construed in accordance with the laws of [Your Jurisdiction], without regard to its conflict of law provisions.

## 16. Dispute Resolution

Any disputes arising out of these Terms or your use of the Service shall be resolved through binding arbitration in accordance with the rules of [Arbitration Organization].

## 17. Severability

If any provision of these Terms is found to be unenforceable or invalid, that provision will be limited or eliminated to the minimum extent necessary, and the remaining provisions will remain in full force and effect.

## 18. Entire Agreement

These Terms, together with our Privacy Policy, constitute the entire agreement between you and MoneyBrain regarding the Service.

## 19. Contact Information

If you have any questions about these Terms, please contact us at:
- Email: support@moneybrain.app
- Website: https://moneybrain.app

---

**Last Updated: February 5, 2026**"
            },
            new LegalDocument
            {
                Id = 2,
                Type = "Privacy",
                Version = "1.0",
                EffectiveDate = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),
                IsMaterialChange = false,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Content = @"# MoneyBrain Privacy Policy

**Effective Date: February 5, 2026**
**Version: 1.0**

## 1. Introduction

This Privacy Policy describes how MoneyBrain (""we,"" ""us,"" or ""our"") collects, uses, and shares your personal information when you use our personal finance management application (""the Service"").

## 2. Information We Collect

### 2.1 Information You Provide

- **Account Information**: Email address, name, password
- **Financial Data**: Transaction details, account balances, budgets, categories, payees
- **Profile Information**: Currency preferences, timezone, date format settings

### 2.2 Automatically Collected Information

- **Usage Data**: Pages viewed, features used, time spent on the Service
- **Device Information**: IP address, browser type, operating system
- **Cookies and Tracking**: We use cookies and similar technologies to enhance your experience

### 2.3 Information from Third Parties

If you connect third-party services (e.g., bank integrations, payment processors), we may receive information from those services in accordance with their privacy policies.

## 3. How We Use Your Information

We use the information we collect to:

- **Provide the Service**: Process transactions, display budgets, generate reports
- **Improve the Service**: Analyze usage patterns, develop new features
- **Communicate with You**: Send service updates, respond to inquiries, provide customer support
- **Security**: Detect and prevent fraud, unauthorized access, and other malicious activities
- **Legal Compliance**: Comply with legal obligations and enforce our Terms of Service

## 4. Legal Basis for Processing (GDPR)

If you are in the European Economic Area (EEA), our legal basis for processing your personal information includes:

- **Consent**: You have given us explicit consent to process your information
- **Contract**: Processing is necessary to perform our contract with you
- **Legal Obligation**: Processing is required by law
- **Legitimate Interest**: Processing is in our legitimate business interests and does not override your rights

## 5. Data Sharing and Disclosure

We do not sell your personal information. We may share your information in the following circumstances:

### 5.1 Service Providers

We may share your information with third-party service providers who perform services on our behalf (e.g., hosting, analytics, customer support). These providers are contractually obligated to protect your information.

### 5.2 Legal Requirements

We may disclose your information if required to do so by law or in response to valid requests by public authorities (e.g., court orders, subpoenas).

### 5.3 Business Transfers

In the event of a merger, acquisition, or sale of assets, your information may be transferred to the acquiring entity.

### 5.4 With Your Consent

We may share your information with third parties when you explicitly consent to such sharing.

## 6. Data Security

We implement appropriate technical and organizational measures to protect your personal information against unauthorized access, alteration, disclosure, or destruction. However, no method of transmission over the Internet or electronic storage is 100% secure.

## 7. Data Retention

We retain your personal information for as long as your account is active or as needed to provide the Service. If you close your account, we will delete or anonymize your data within 90 days, unless we are required to retain it for legal or regulatory purposes.

## 8. Your Rights

Depending on your location, you may have the following rights regarding your personal information:

### 8.1 Access and Portability

You have the right to request a copy of the personal information we hold about you and to receive it in a portable format.

### 8.2 Correction

You have the right to request correction of inaccurate or incomplete personal information.

### 8.3 Deletion

You have the right to request deletion of your personal information, subject to certain legal exceptions.

### 8.4 Restriction and Objection

You have the right to restrict or object to our processing of your personal information in certain circumstances.

### 8.5 Withdraw Consent

If we process your information based on consent, you have the right to withdraw that consent at any time.

To exercise any of these rights, please contact us at privacy@moneybrain.app.

## 9. International Data Transfers

If you are located outside the country where our servers are located, your information may be transferred to and processed in that country. We ensure appropriate safeguards are in place for such transfers in compliance with applicable data protection laws.

## 10. Children's Privacy

MoneyBrain is not intended for use by individuals under the age of 18. We do not knowingly collect personal information from children. If we become aware that we have collected information from a child, we will take steps to delete it promptly.

## 11. California Privacy Rights (CCPA)

If you are a California resident, you have the right to:

- **Know**: Request disclosure of the categories and specific pieces of personal information we collect
- **Delete**: Request deletion of your personal information
- **Opt-Out**: Opt-out of the sale of your personal information (we do not sell personal information)
- **Non-Discrimination**: Not be discriminated against for exercising your privacy rights

To exercise these rights, contact us at privacy@moneybrain.app.

## 12. Cookies and Tracking Technologies

We use cookies and similar tracking technologies to:

- Authenticate users and prevent fraud
- Remember your preferences and settings
- Analyze usage and improve the Service

You can control cookies through your browser settings. Disabling cookies may limit your ability to use certain features of the Service.

## 13. Third-Party Links

The Service may contain links to third-party websites or services. We are not responsible for the privacy practices of these third parties. We encourage you to review their privacy policies before providing any personal information.

## 14. Changes to This Privacy Policy

We may update this Privacy Policy from time to time. If we make material changes, we will notify you by email or through a prominent notice on the Service. Your continued use of the Service after such changes constitutes your acceptance of the updated Privacy Policy.

## 15. Contact Us

If you have any questions or concerns about this Privacy Policy, please contact us:

- **Email**: privacy@moneybrain.app
- **Website**: https://moneybrain.app
- **Address**: [Your Business Address]

## 16. Data Protection Officer

If required by law, we have appointed a Data Protection Officer (DPO). You may contact our DPO at dpo@moneybrain.app.

---

**Last Updated: February 5, 2026**"
            }
        );
    }
}