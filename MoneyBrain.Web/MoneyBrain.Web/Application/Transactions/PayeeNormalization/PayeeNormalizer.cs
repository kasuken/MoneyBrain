using System.Text;
using System.Text.RegularExpressions;

namespace MoneyBrain.Web.Application.Transactions.PayeeNormalization;

/// <summary>
/// Utility for normalizing and comparing payee names
/// </summary>
public static class PayeeNormalizer
{
    /// <summary>
    /// Normalize a payee name for comparison and storage
    /// </summary>
    public static string Normalize(string payeeName)
    {
        if (string.IsNullOrWhiteSpace(payeeName))
            return string.Empty;

        var normalized = payeeName;

        // Remove common prefixes/suffixes
        normalized = RemoveCommonAffixes(normalized);

        // Remove special characters and extra whitespace
        normalized = Regex.Replace(normalized, @"[^\w\s]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ");

        // Trim and convert to title case for consistency
        normalized = normalized.Trim();

        return normalized;
    }

    /// <summary>
    /// Get a normalized key for finding duplicates (lowercase, no spaces)
    /// </summary>
    public static string GetNormalizedKey(string payeeName)
    {
        var normalized = Normalize(payeeName);
        return Regex.Replace(normalized.ToLowerInvariant(), @"\s+", "");
    }

    /// <summary>
    /// Calculate similarity score between two payee names (0.0 to 1.0)
    /// </summary>
    public static double CalculateSimilarity(string name1, string name2)
    {
        if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
            return 0.0;

        var key1 = GetNormalizedKey(name1);
        var key2 = GetNormalizedKey(name2);

        if (key1 == key2)
            return 1.0;

        // Use Levenshtein distance for similarity
        var distance = LevenshteinDistance(key1, key2);
        var maxLength = Math.Max(key1.Length, key2.Length);

        if (maxLength == 0)
            return 0.0;

        return 1.0 - ((double)distance / maxLength);
    }

    /// <summary>
    /// Check if two payee names are likely duplicates
    /// </summary>
    public static bool AreLikelyDuplicates(string name1, string name2, double threshold = 0.85)
    {
        var similarity = CalculateSimilarity(name1, name2);
        return similarity >= threshold;
    }

    private static string RemoveCommonAffixes(string name)
    {
        // Remove common business suffixes
        var suffixes = new[] { " Inc", " LLC", " Ltd", " Co", " Corp", " Corporation", " Company" };
        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - suffix.Length);
            }
        }

        // Remove store numbers and location codes
        name = Regex.Replace(name, @"#\d+", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"Store\s*\d+", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"Location\s*\d+", "", RegexOptions.IgnoreCase);

        return name;
    }

    private static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return string.IsNullOrEmpty(target) ? 0 : target.Length;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        var sourceLength = source.Length;
        var targetLength = target.Length;
        var distance = new int[sourceLength + 1, targetLength + 1];

        for (var i = 0; i <= sourceLength; i++)
            distance[i, 0] = i;

        for (var j = 0; j <= targetLength; j++)
            distance[0, j] = j;

        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                distance[i, j] = Math.Min(
                    Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                    distance[i - 1, j - 1] + cost);
            }
        }

        return distance[sourceLength, targetLength];
    }
}
