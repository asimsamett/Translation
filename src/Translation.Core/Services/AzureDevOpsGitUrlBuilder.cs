using Translation.Core.Options;

namespace Translation.Core.Services;

public static class AzureDevOpsGitUrlBuilder
{
    public static string BuildCloneUrl(AzureDevOpsOptions options, bool embedPat)
    {
        if (!string.IsNullOrWhiteSpace(options.CloneUrl))
            return options.CloneUrl.Trim();

        var organization = options.Organization.Trim();
        var project = NormalizeSegment(options.Project);
        var repository = NormalizeSegment(options.Repository);

        if (string.IsNullOrWhiteSpace(organization)
            || string.IsNullOrWhiteSpace(project)
            || string.IsNullOrWhiteSpace(repository))
        {
            throw new InvalidOperationException(
                "AzureDevOps:Organization, Project ve Repository ayarlanmalıdır (veya AzureDevOps:CloneUrl verin).");
        }

        var baseUrl = string.IsNullOrWhiteSpace(options.GitBaseUrl)
            ? "https://dev.azure.com"
            : options.GitBaseUrl.TrimEnd('/');

        var baseUri = new Uri($"{baseUrl}/", UriKind.Absolute);
        var relativePath = $"{organization}/{EncodePathSegment(project)}/_git/{EncodePathSegment(repository)}";
        var targetUri = new Uri(baseUri, relativePath);

        if (!embedPat || string.IsNullOrWhiteSpace(options.PersonalAccessToken))
            return targetUri.ToString();

        var builder = new UriBuilder(targetUri)
        {
            UserName = string.IsNullOrWhiteSpace(options.GitUsername) ? "pat" : options.GitUsername,
            Password = options.PersonalAccessToken
        };
        return builder.Uri.AbsoluteUri;
    }

    public static string NormalizeRepositoryName(string repository) => NormalizeSegment(repository);

    public static string ToSafeDisplayUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        var at = url.IndexOf('@');
        return at >= 0 ? $"https://***@{url[(at + 1)..]}" : url;
    }

    private static string NormalizeSegment(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4];
        return trimmed;
    }

    private static string EncodePathSegment(string segment) =>
        Uri.EscapeDataString(segment);
}
