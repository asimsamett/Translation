using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Translation.Core.Options;

namespace Translation.Core.Services;

public sealed class BranchNamingService
{
    private static readonly Regex InvalidBranchChars = new(@"[^a-zA-Z0-9._/-]+", RegexOptions.Compiled);

    private readonly BranchNamingOptions _options;

    public BranchNamingService(IOptions<BranchNamingOptions> options) => _options = options.Value;

    public string Suggest(string? userName, string? suffix = null)
    {
        var now = DateTime.Now;
        var user = SanitizeSegment(userName ?? "dev");
        var suffixPart = string.IsNullOrWhiteSpace(suffix) ? null : SanitizeSegment(suffix);

        var name = _options.Pattern
            .Replace("{user}", user, StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", now.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{datetime}", now.ToString("yyyyMMdd-HHmm"), StringComparison.OrdinalIgnoreCase)
            .Replace("{suffix}", suffixPart ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        name = name.Replace("//", "/", StringComparison.Ordinal);
        name = name.Trim('/');
        return TrimToMaxLength(SanitizeBranchName(name));
    }

    public void ValidateOrThrow(string branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
            throw new ArgumentException("Branch adı boş olamaz.");

        var trimmed = branchName.Trim();
        if (trimmed != branchName)
            throw new ArgumentException("Branch adının başında veya sonunda boşluk olamaz.");

        if (trimmed.StartsWith('.') || trimmed.StartsWith('/') || trimmed.EndsWith('.') || trimmed.EndsWith('/'))
            throw new ArgumentException("Branch adı . veya / ile başlayıp bitemez.");

        if (trimmed.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Branch adı '..' içeremez.");

        if (trimmed.Contains(' '))
            throw new ArgumentException("Branch adında boşluk olamaz.");

        if (trimmed.Length > _options.MaxLength)
            throw new ArgumentException($"Branch adı en fazla {_options.MaxLength} karakter olabilir.");
    }

    private string SanitizeBranchName(string raw)
    {
        var cleaned = InvalidBranchChars.Replace(raw, "-");
        cleaned = Regex.Replace(cleaned, "-{2,}", "-");
        cleaned = Regex.Replace(cleaned, "/{2,}", "/");
        return cleaned.Trim('-', '/');
    }

    private static string SanitizeSegment(string value)
    {
        var segment = value.Trim().ToLowerInvariant();
        var at = segment.IndexOf('@');
        if (at > 0)
            segment = segment[..at];
        segment = segment.Replace('\\', '-').Replace(' ', '-');
        return InvalidBranchChars.Replace(segment, "-").Trim('-');
    }

    private string TrimToMaxLength(string name)
    {
        if (name.Length <= _options.MaxLength)
            return name;

        var parts = name.Split('/');
        while (parts.Length > 1 && string.Join('/', parts).Length > _options.MaxLength)
            parts = parts[..^1];

        var joined = string.Join('/', parts);
        return joined.Length <= _options.MaxLength ? joined : joined[.._options.MaxLength].TrimEnd('-', '/');
    }
}
