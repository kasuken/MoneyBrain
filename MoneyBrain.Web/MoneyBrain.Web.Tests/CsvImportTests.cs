using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Reporting.CsvExport;
using MoneyBrain.Web.Application.Transactions.CsvImport;
using MoneyBrain.Web.Application.Transactions.Filtering;
using MoneyBrain.Web.Application.Transactions.PayeeNormalization;
using MoneyBrain.Web.Domain.Enums;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>CSV parsing, EU number and date formats, duplicate detection and the export round trip.</summary>
[TestClass]
public sealed class CsvImportTests
{
    // ---- RFC 4180 parser ----

    /// <summary>Quoted fields can contain delimiters, doubled quotes and line breaks.</summary>
    [TestMethod]
    public void Parser_handles_quoted_newlines_quotes_and_crlf()
    {
        var rows = CsvFormat.Parse(
            "Date,Memo,Amount\r\n2026-01-02,\"line one\nline \"\"two\"\", with comma\",5\r\n\r\n2026-01-03,plain,6\r\n", ',');

        Assert.AreEqual(3, rows.Count);
        Assert.AreEqual("line one\nline \"two\", with comma", rows[1][1]);
        Assert.AreEqual("6", rows[2][2]);
    }

    /// <summary>A byte order mark and a missing final newline are tolerated.</summary>
    [TestMethod]
    public void Parser_ignores_bom_and_missing_trailing_newline()
    {
        var rows = CsvFormat.Parse("﻿a,b\n1,2");

        CollectionAssert.AreEqual(new[] { "a", "b" }, rows[0]);
        CollectionAssert.AreEqual(new[] { "1", "2" }, rows[1]);
    }

    /// <summary>EU bank exports use semicolons; the delimiter is detected outside quotes.</summary>
    [TestMethod]
    [DataRow("Datum;Betrag;Zweck\n01.02.2026;12,50;Miete", ';')]
    [DataRow("a\tb\tc\n1\t2\t3", '\t')]
    [DataRow("a|b|c\n1|2|3", '|')]
    [DataRow("\"a;b\",c,d\n1,2,3", ',')]
    [DataRow("single\nvalue", ',')]
    public void Delimiter_is_detected(string content, char expected) =>
        Assert.AreEqual(expected, CsvFormat.DetectDelimiter(content));

    // ---- amounts ----

    /// <summary>EU amounts use a decimal comma and optional dot grouping.</summary>
    [TestMethod]
    [DataRow("12,50", "12.50")]
    [DataRow("1.234,56", "1234.56")]
    [DataRow("-1.234,56", "-1234.56")]
    [DataRow("1.234.567,89", "1234567.89")]
    [DataRow("12,50-", "-12.50")]
    [DataRow("(12,50)", "-12.50")]
    [DataRow("€ 1.234,56", "1234.56")]
    [DataRow("1 234,56", "1234.56")]
    [DataRow("1234", "1234")]
    public void European_amounts_are_parsed(string text, string expected)
    {
        Assert.IsTrue(CsvFormat.TryParseAmount(text, CsvAmountFormat.European, out var amount), text);
        Assert.AreEqual(decimal.Parse(expected, CultureInfo.InvariantCulture), amount);
    }

    /// <summary>Standard amounts use a decimal point and comma grouping.</summary>
    [TestMethod]
    [DataRow("12.50", "12.50")]
    [DataRow("1,234.56", "1234.56")]
    [DataRow("-$1,234.56", "-1234.56")]
    [DataRow("$12", "12")]
    [DataRow("1,234", "1234")]
    public void Standard_amounts_are_parsed(string text, string expected)
    {
        Assert.IsTrue(CsvFormat.TryParseAmount(text, CsvAmountFormat.Standard, out var amount), text);
        Assert.AreEqual(decimal.Parse(expected, CultureInfo.InvariantCulture), amount);
    }

    /// <summary>The old parser read "12,50" as 1250 and "1.234,56" as 1.23456 without an error. Now it is rejected.</summary>
    [TestMethod]
    [DataRow("12,50", CsvAmountFormat.Standard)]
    [DataRow("1.234,56", CsvAmountFormat.Standard)]
    [DataRow("12.50", CsvAmountFormat.European)]
    [DataRow("1,234.56", CsvAmountFormat.European)]
    [DataRow("abc", CsvAmountFormat.Standard)]
    [DataRow("", CsvAmountFormat.Standard)]
    [DataRow("1,23,456", CsvAmountFormat.Standard)]
    [DataRow("12..5", CsvAmountFormat.Standard)]
    public void Amounts_in_the_wrong_format_are_errors_not_guesses(string text, CsvAmountFormat format) =>
        Assert.IsFalse(CsvFormat.TryParseAmount(text, format, out _), text);

    // ---- dates ----

    /// <summary>The chosen format is applied exactly; there is no day/month swap fallback.</summary>
    [TestMethod]
    public void Dates_follow_the_chosen_format_without_swapping_day_and_month()
    {
        Assert.IsTrue(CsvFormat.TryParseDate("03/04/2026", "dd/MM/yyyy", out var european));
        Assert.AreEqual(new DateTime(2026, 4, 3), european);
        Assert.IsTrue(CsvFormat.TryParseDate("03/04/2026", "MM/dd/yyyy", out var american));
        Assert.AreEqual(new DateTime(2026, 3, 4), american);
        Assert.IsTrue(CsvFormat.TryParseDate("15.01.2026", "dd.MM.yyyy", out var dotted));
        Assert.AreEqual(new DateTime(2026, 1, 15), dotted);

        // 25/12/2026 is not a valid MM/dd/yyyy date; the old fallback would have reinterpreted it.
        Assert.IsFalse(CsvFormat.TryParseDate("25/12/2026", "MM/dd/yyyy", out _));
        Assert.IsFalse(CsvFormat.TryParseDate("not a date", "dd/MM/yyyy", out _));
    }

    /// <summary>ISO dates are unambiguous and always accepted, because MoneyBrain exports them.</summary>
    [TestMethod]
    public void Iso_dates_are_always_accepted()
    {
        Assert.IsTrue(CsvFormat.TryParseDate("2026-03-04", "dd/MM/yyyy", out var date));
        Assert.AreEqual(new DateTime(2026, 3, 4), date);
    }

    // ---- service ----

    /// <summary>A semicolon-separated EU file with decimal commas and dd.MM.yyyy dates imports correctly.</summary>
    [TestMethod]
    public async Task Eu_file_imports_with_correct_amounts_and_dates()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var importer = CreateImporter(database);
        const string csv = "Datum;Betrag;Empfänger;Verwendungszweck\r\n" +
                           "15.01.2026;-1.234,56;Vermieter;\"Miete\nJanuar\"\r\n" +
                           "16.01.2026;-12,50;Bäckerei;Brot\r\n";
        var mapping = Mapping(database, dateFormat: "dd.MM.yyyy", format: CsvAmountFormat.European);

        var result = await importer.ImportTransactionsAsync(FinanceDatabase.Owner, csv, mapping);

        Assert.AreEqual(2, result.ImportedCount, string.Join("; ", result.Errors));
        await using var context = database.CreateDbContext();
        var stored = await context.Transactions.OrderBy(t => t.Date).ToListAsync();
        Assert.AreEqual(-1234.56m, stored[0].Amount);
        Assert.AreEqual(new DateTime(2026, 1, 15), stored[0].Date);
        Assert.AreEqual("Miete\nJanuar", stored[0].Memo);
        Assert.AreEqual(-12.50m, stored[1].Amount);
    }

    /// <summary>A bad amount is reported on its row and nothing is silently misread.</summary>
    [TestMethod]
    public async Task Preview_flags_amounts_in_the_wrong_format()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var importer = CreateImporter(database);
        var mapping = Mapping(database, dateFormat: "dd.MM.yyyy", format: CsvAmountFormat.Standard);

        var rows = await importer.PreviewImportAsync(FinanceDatabase.Owner,
            "Datum;Betrag;Empfänger\n15.01.2026;12,50;Shop\n", mapping);

        Assert.AreEqual(1, rows.Count);
        Assert.IsTrue(rows[0].HasErrors);
        Assert.IsNull(rows[0].Amount);
    }

    /// <summary>Importing the same file twice does not create duplicates.</summary>
    [TestMethod]
    public async Task Reimporting_a_file_skips_duplicates()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var importer = CreateImporter(database);
        const string csv = "Date,Amount,Payee\n2026-01-15,-10.00,Shop\n2026-01-16,-20.00,Cafe\n";
        var mapping = Mapping(database);

        var first = await importer.ImportTransactionsAsync(FinanceDatabase.Owner, csv, mapping);
        var second = await importer.ImportTransactionsAsync(FinanceDatabase.Owner, csv, mapping);

        Assert.AreEqual(2, first.ImportedCount);
        Assert.AreEqual(0, second.ImportedCount);
        Assert.AreEqual(2, second.DuplicateCount);
        Assert.AreEqual(2, second.SkippedCount);
        Assert.IsTrue(second.Success, "Duplicates are not errors");
        await using var context = database.CreateDbContext();
        Assert.AreEqual(2, await context.Transactions.CountAsync());
    }

    /// <summary>Two identical purchases in one file are both real; only existing ones count as duplicates.</summary>
    [TestMethod]
    public async Task Identical_rows_in_one_file_are_not_duplicates_of_each_other()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var importer = CreateImporter(database);
        const string csv = "Date,Amount,Payee\n2026-01-15,-3.50,Coffee\n2026-01-15,-3.50,Coffee\n";

        var result = await importer.ImportTransactionsAsync(FinanceDatabase.Owner, csv, Mapping(database));

        Assert.AreEqual(2, result.ImportedCount);
        Assert.AreEqual(0, result.DuplicateCount);
    }

    /// <summary>Duplicate skipping can be switched off, and it only compares within the target account.</summary>
    [TestMethod]
    public async Task Duplicate_skipping_is_optional_and_scoped_to_the_account()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var importer = CreateImporter(database);
        const string csv = "Date,Amount,Payee\n2026-01-15,-10.00,Shop\n";
        await importer.ImportTransactionsAsync(FinanceDatabase.Owner, csv, Mapping(database));

        var other = Mapping(database);
        other.AccountId = database.Savings.Id;
        var intoOtherAccount = await importer.ImportTransactionsAsync(FinanceDatabase.Owner, csv, other);
        var noSkip = Mapping(database);
        noSkip.SkipDuplicates = false;
        var withoutSkipping = await importer.ImportTransactionsAsync(FinanceDatabase.Owner, csv, noSkip);

        Assert.AreEqual(1, intoOtherAccount.ImportedCount);
        Assert.AreEqual(1, withoutSkipping.ImportedCount);
    }

    /// <summary>The shipped sample file still parses with the default mapping.</summary>
    [TestMethod]
    public async Task Sample_file_previews_without_errors()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var importer = CreateImporter(database);
        var csv = await File.ReadAllTextAsync(FindUp("samples", "sample-transactions.csv"));
        var mapping = Mapping(database, dateFormat: "MM/dd/yyyy");
        mapping.DateColumn = 0;
        mapping.PayeeColumn = 1;
        mapping.AmountColumn = 2;
        mapping.CategoryColumn = 3;
        mapping.ClearedColumn = 4;
        mapping.MemoColumn = null;

        var rows = await importer.PreviewImportAsync(FinanceDatabase.Owner, csv, mapping);

        Assert.IsTrue(rows.Count > 0);
        Assert.IsFalse(rows.Any(r => r.HasErrors), string.Join("; ", rows.SelectMany(r => r.Errors)));
    }

    // ---- export round trip ----

    /// <summary>Exported transactions can be imported again with ISO dates and decimal points, including awkward text.</summary>
    [TestMethod]
    public async Task Exported_transactions_round_trip_through_import()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var payees = new PayeeService(database);
        var payee = await payees.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "Acme");
        await database.Transactions.CreateTransactionAsync(FinanceDatabase.Owner, database.Checking.Id,
            new DateTime(2026, 1, 15), -42.10m, payee.Id, database.Category.Id,
            "=SUM(A1) \"quoted\", with\nnewline", TransactionStatus.Posted, true, "REF-1", "tag1,tag2");
        await database.Transactions.CreateTransactionAsync(FinanceDatabase.Owner, database.Checking.Id,
            new DateTime(2026, 1, 16), -5m, null, null, "-dash memo", TransactionStatus.Pending, false, null, null);
        var all = await database.Transactions.SearchTransactionsAsync(FinanceDatabase.Owner, new TransactionFilter());

        var csv = Encoding.UTF8.GetString(new CsvExportService().ExportTransactionsToCsv(all));

        StringAssert.StartsWith(csv,
            "﻿Date,Account,Amount,Payee,Category,Memo,ReferenceNumber,Cleared,Status,Reconciled,Tags,TransferAccount,Splits");
        var headers = CsvFormat.Parse(csv)[0];
        var mapping = new TransactionCsvColumnMapping
        {
            AccountId = database.Savings.Id,
            DateColumn = headers.IndexOf("Date"),
            AmountColumn = headers.IndexOf("Amount"),
            PayeeColumn = headers.IndexOf("Payee"),
            CategoryColumn = headers.IndexOf("Category"),
            MemoColumn = headers.IndexOf("Memo"),
            ReferenceNumberColumn = headers.IndexOf("ReferenceNumber"),
            ClearedColumn = headers.IndexOf("Cleared"),
            DateFormat = "yyyy-MM-dd"
        };

        var result = await CreateImporter(database).ImportTransactionsAsync(FinanceDatabase.Owner, csv, mapping);

        Assert.AreEqual(2, result.ImportedCount, string.Join("; ", result.Errors));
        await using var context = database.CreateDbContext();
        var imported = await context.Transactions.Where(t => t.AccountId == database.Savings.Id)
            .OrderBy(t => t.Date).ToListAsync();
        Assert.AreEqual(-42.10m, imported[0].Amount);
        Assert.AreEqual("=SUM(A1) \"quoted\", with\nnewline", imported[0].Memo);
        Assert.AreEqual("REF-1", imported[0].ReferenceNumber);
        Assert.IsTrue(imported[0].IsCleared);
        Assert.AreEqual("-dash memo", imported[1].Memo);
        Assert.AreEqual(-5m, imported[1].Amount);
    }

    /// <summary>Text that a spreadsheet could run as a formula is neutralized in the file and restored on import.</summary>
    [TestMethod]
    public void Exported_text_neutralizes_spreadsheet_formulas()
    {
        Assert.AreEqual("\"'=cmd\"", CsvFormat.EscapeText("=cmd"));
        Assert.AreEqual("\"'@x\"", CsvFormat.EscapeText("@x"));
        Assert.AreEqual("\"plain\"", CsvFormat.EscapeText("plain"));
        Assert.AreEqual("=cmd", CsvFormat.UnescapeText("'=cmd"));
        Assert.AreEqual("'quoted", CsvFormat.UnescapeText("'quoted"));
    }

    private static TransactionCsvImportService CreateImporter(FinanceDatabase database) =>
        new(database, database.Transactions, new PayeeService(database),
            NullLogger<TransactionCsvImportService>.Instance);

    private static TransactionCsvColumnMapping Mapping(FinanceDatabase database, string dateFormat = "yyyy-MM-dd",
        CsvAmountFormat format = CsvAmountFormat.Standard) => new()
    {
        AccountId = database.Checking.Id,
        DateColumn = 0,
        AmountColumn = 1,
        PayeeColumn = 2,
        MemoColumn = 3,
        DateFormat = dateFormat,
        AmountFormat = format
    };

    private static string FindUp(string folder, string file)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, folder, file);
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException(file);
    }
}
