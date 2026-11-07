using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MoneyBrain.Web.Application.Transactions.CsvImport;

/// <summary>How decimal and grouping separators are written in the CSV amount column.</summary>
public enum CsvAmountFormat
{
    /// <summary>Decimal point, optional comma grouping: <c>1,234.56</c>.</summary>
    Standard = 0,

    /// <summary>Decimal comma, optional dot grouping: <c>1.234,56</c> (common in EU bank exports).</summary>
    European = 1
}

/// <summary>An RFC 4180 CSV reader plus strict amount and date parsing used by the transaction import.</summary>
public static partial class CsvFormat
{
    private static readonly char[] CandidateDelimiters = [',', ';', '\t', '|'];

    [GeneratedRegex(@"^(\d{1,3}(,\d{3})+|\d+)?(\.\d+)?$")]
    private static partial Regex StandardPattern();

    [GeneratedRegex(@"^(\d{1,3}(\.\d{3})+|\d+)?(,\d+)?$")]
    private static partial Regex EuropeanPattern();

    /// <summary>
    /// Picks the delimiter that appears most often outside quotes in the first record. Falls back to a comma.
    /// </summary>
    public static char DetectDelimiter(string content)
    {
        var counts = new int[CandidateDelimiters.Length];
        var inQuotes = false;
        var start = content.Length > 0 && content[0] == '﻿' ? 1 : 0;

        for (var i = start; i < content.Length; i++)
        {
            var c = content[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < content.Length && content[i + 1] == '"')
                    i++;
                else
                    inQuotes = !inQuotes;
            }
            else if (!inQuotes && (c == '\n' || c == '\r'))
            {
                if (counts.Any(n => n > 0)) break;
            }
            else if (!inQuotes)
            {
                var index = Array.IndexOf(CandidateDelimiters, c);
                if (index >= 0) counts[index]++;
            }
        }

        var best = 0;
        for (var i = 1; i < counts.Length; i++)
        {
            if (counts[i] > counts[best]) best = i;
        }

        return counts[best] == 0 ? ',' : CandidateDelimiters[best];
    }

    /// <summary>
    /// Parses CSV text per RFC 4180: quoted fields may contain delimiters, doubled quotes and line breaks;
    /// CRLF, LF and CR end a record. Records that contain only blank fields are skipped.
    /// </summary>
    /// <param name="content">The CSV text. A leading byte order mark is ignored.</param>
    /// <param name="delimiter">The field delimiter, or <c>null</c> to detect it.</param>
    public static List<List<string>> Parse(string content, char? delimiter = null)
    {
        var separator = delimiter ?? DetectDelimiter(content);
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldWasQuoted = false;

        void EndField()
        {
            row.Add(fieldWasQuoted ? field.ToString() : field.ToString().Trim());
            field.Clear();
            fieldWasQuoted = false;
        }

        void EndRow()
        {
            EndField();
            if (row.Any(f => !string.IsNullOrWhiteSpace(f)))
                rows.Add(row);
            row = [];
        }

        var i = content.Length > 0 && content[0] == '﻿' ? 1 : 0;
        for (; i < content.Length; i++)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"' && field.ToString().Trim().Length == 0)
            {
                field.Clear();
                inQuotes = true;
                fieldWasQuoted = true;
            }
            else if (c == separator)
            {
                EndField();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                EndRow();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0 || fieldWasQuoted)
            EndRow();

        return rows;
    }

    /// <summary>
    /// Parses an amount strictly in the given format. Returns false rather than guessing, so <c>12,50</c> is never
    /// silently read as 1250 in <see cref="CsvAmountFormat.Standard"/> mode. Currency symbols, spaces, apostrophe
    /// grouping, a trailing minus (<c>12.50-</c>) and accounting parentheses (<c>(12.50)</c>) are accepted.
    /// </summary>
    public static bool TryParseAmount(string? text, CsvAmountFormat format, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var value = text.Trim();
        var negative = false;

        if (value.StartsWith('(') && value.EndsWith(')'))
        {
            negative = true;
            value = value[1..^1];
        }

        var cleaned = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || c == '\'' || c == '’' ||
                char.GetUnicodeCategory(c) == UnicodeCategory.CurrencySymbol)
                continue;
            cleaned.Append(c == '−' ? '-' : c);
        }

        value = cleaned.ToString();
        if (value.EndsWith('-'))
        {
            negative = !negative;
            value = value[..^1];
        }
        else if (value.StartsWith('-'))
        {
            negative = !negative;
            value = value[1..];
        }
        else if (value.StartsWith('+'))
        {
            value = value[1..];
        }

        if (value.Length == 0) return false;

        var pattern = format == CsvAmountFormat.European ? EuropeanPattern() : StandardPattern();
        if (!pattern.IsMatch(value)) return false;

        var normalized = format == CsvAmountFormat.European
            ? value.Replace(".", "").Replace(',', '.')
            : value.Replace(",", "");

        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount))
            return false;

        if (negative) amount = -amount;
        return true;
    }
    /// <summary>
    /// Quotes a text field for CSV output and neutralizes spreadsheet formulas: text starting with
    /// <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, a tab or a carriage return is prefixed with an apostrophe.
    /// <see cref="UnescapeText"/> reverses that on import, so exports round-trip losslessly.
    /// Line breaks inside the text are preserved (RFC 4180 quoted field).
    /// </summary>
    public static string EscapeText(string? text)
    {
        text ??= string.Empty;
        var trimmed = text.TrimStart();
        if (text.Length > 0 &&
            ((trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@') || text[0] is '\t' or '\r'))
            text = "'" + text;

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Removes the apostrophe <see cref="EscapeText"/> adds in front of formula-like text.</summary>
    public static string? UnescapeText(string? text)
    {
        if (text is { Length: > 1 } && text[0] == '\'')
        {
            var rest = text[1..];
            var trimmed = rest.TrimStart();
            if ((trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@') || rest[0] is '\t' or '\r')
                return rest;
        }

        return text;
    }


    /// <summary>
    /// Parses a date strictly with the user-chosen format. ISO <c>yyyy-MM-dd</c> is always accepted too because it is
    /// unambiguous and is what MoneyBrain exports. There is deliberately no culture-guessing fallback, which could
    /// silently swap day and month.
    /// </summary>
    public static bool TryParseDate(string? text, string? format, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.Trim();

        if (!string.IsNullOrWhiteSpace(format) &&
            DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
