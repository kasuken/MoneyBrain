using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBrain.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedTipsAndDisclaimers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = DateTime.UtcNow;

            // Seed Educational Tips
            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Emergency Fund Basics", "An emergency fund typically covers 3-6 months of essential living expenses. This safety net helps manage unexpected costs like medical bills or urgent repairs without disrupting long-term financial goals.", "saving", 1, true, "Tip_Saving_Emergency", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "High-Yield Savings Accounts", "High-yield savings accounts offer higher interest rates than traditional savings accounts while maintaining liquidity and security. Comparing rates across institutions can increase savings growth.", "saving", 2, true, "Tip_Saving_Interest", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Setting Savings Goals", "Specific, measurable savings goals with defined timelines increase the likelihood of achieving desired financial outcomes. Breaking larger goals into smaller milestones makes progress more visible.", "saving", 3, true, "Tip_Saving_Goals", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Expense Tracking Benefits", "Regular expense tracking reveals spending patterns and helps identify areas where small, frequent purchases accumulate into significant amounts over time.", "spending", 4, true, "Tip_Spending_Tracking", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Needs vs Wants Analysis", "Distinguishing between essential expenses and discretionary spending provides clarity on where budget adjustments can be made without affecting basic quality of life.", "spending", 5, true, "Tip_Spending_Needs", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Subscription Management", "Regularly reviewing recurring subscriptions helps identify services that are no longer used or valued. Small monthly charges can accumulate to significant annual expenses.", "spending", 6, true, "Tip_Spending_Subscriptions", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Understanding Compound Interest", "Compound interest allows investments to grow exponentially over time, as returns generate additional returns. Starting early, even with smaller amounts, can have a substantial impact over decades.", "investing", 7, true, "Tip_Investing_Compound", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Investment Diversification", "Spreading investments across different asset types and sectors helps manage risk. A diversified portfolio reduces the impact of poor performance in any single investment.", "investing", 8, true, "Tip_Investing_Diversification", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Envelope Budgeting Method", "Envelope budgeting assigns a fixed amount to each spending category each month. When a category's allocation is depleted, no additional spending occurs in that category until the next period.", "budgeting", 9, true, "Tip_Budgeting_Envelope", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Zero-Based Budgeting", "Zero-based budgeting allocates every dollar of income to a specific purpose—expenses, savings, or debt repayment—ensuring intentional use of available resources.", "budgeting", 10, true, "Tip_Budgeting_ZeroBased", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Financial Automation", "Automating savings transfers and bill payments reduces the mental effort required for financial management and helps ensure consistent progress toward financial goals.", "general", 11, true, "Tip_General_Automation", now });

            migrationBuilder.InsertData(
                table: "EducationalTips",
                columns: new[] { "Title", "Content", "Category", "DisplayOrder", "IsActive", "LocalizationKey", "CreatedAt" },
                values: new object[] { "Debt Repayment Strategies", "Common debt repayment approaches include the avalanche method (highest interest first) and the snowball method (smallest balance first). Each has different psychological and financial benefits.", "general", 12, true, "Tip_General_Debt", now });

            // Seed Feature Disclaimer
            migrationBuilder.InsertData(
                table: "FeatureDisclaimers",
                columns: new[] { "Feature", "DisclaimerText", "LocalizationKey", "IsActive", "CreatedAt" },
                values: new object[] { "EducationalTips", "Educational tips are provided for informational purposes only and do not constitute financial advice. Please consult with a qualified financial advisor for personalized guidance.", "Disclaimer_EducationalTips", true, now });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM EducationalTips WHERE LocalizationKey LIKE 'Tip_%'");
            migrationBuilder.Sql("DELETE FROM FeatureDisclaimers WHERE Feature = 'EducationalTips'");
        }
    }
}
