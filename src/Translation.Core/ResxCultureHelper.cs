using System.Text.RegularExpressions;

namespace Translation.Core;

public static partial class ResxCultureHelper
{
    [GeneratedRegex(@"\.([a-z]{2}(?:-[A-Z]{2})?)\.(?:resx|json)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CultureSuffixRegex();

    public static string? TryGetCulture(string fileName)
    {
        var match = CultureSuffixRegex().Match(fileName);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static string GetCultureLabel(string? culture) =>
        culture is null ? "varsayılan" : culture;
}
