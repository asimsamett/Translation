using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Translation.Core.Models;
using Translation.Core.Options;

namespace Translation.Core.Services;

public sealed class AzureDevOpsService : IAzureDevOpsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly AzureDevOpsOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AzureDevOpsService> _logger;

    public AzureDevOpsService(
        IOptions<AzureDevOpsOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AzureDevOpsService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task EnsureRepositoryAccessibleAsync(CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var url = BuildRepositoryApiUrl();

        using var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(BuildConfigurationHelpMessage(
                "Azure DevOps repository bulunamadı (404)."));
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Azure DevOps repository doğrulanamadı ({(int)response.StatusCode}): {body}");
        }
    }

    public async Task<PullRequestCreateResult> CreatePullRequestAsync(
        PullRequestCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var repositoryId = await GetRepositoryIdAsync(cancellationToken);
        var client = CreateClient();

        var payload = new
        {
            sourceRefName = ToRef(request.SourceBranch),
            targetRefName = ToRef(request.TargetBranch),
            title = request.Title,
            description = BuildPullRequestDescription(request.Description)
        };

        var url = $"{BuildProjectApiBase()}/git/repositories/{repositoryId}/pullrequests?api-version=7.1";

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Azure DevOps PR failed ({Status}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Pull request creation failed: {response.StatusCode}");
        }

        var pr = JsonSerializer.Deserialize<PullRequestResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Invalid pull request response.");

        var prUrl =
            $"{GetWebBaseUrl()}/{_options.Organization}/{_options.Project}/_git/{_options.Repository}/pullrequest/{pr.PullRequestId}";

        return new PullRequestCreateResult(pr.PullRequestId, prUrl, pr.Title ?? request.Title);
    }

    private async Task<string> GetRepositoryIdAsync(CancellationToken cancellationToken)
    {
        var client = CreateClient();
        using var response = await client.GetAsync(BuildRepositoryApiUrl(), cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var repo = JsonSerializer.Deserialize<RepositoryResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Repository lookup failed.");
        return repo.Id;
    }

    private string BuildRepositoryApiUrl()
    {
        var repo = Uri.EscapeDataString(AzureDevOpsGitUrlBuilder.NormalizeRepositoryName(_options.Repository));
        return $"{BuildProjectApiBase()}/git/repositories/{repo}?api-version=7.1";
    }

    private string BuildProjectApiBase() =>
        $"{GetWebBaseUrl()}/{_options.Organization.Trim()}/{Uri.EscapeDataString(_options.Project.Trim())}/_apis";

    private string GetWebBaseUrl() =>
        string.IsNullOrWhiteSpace(_options.GitBaseUrl) ? "https://dev.azure.com" : _options.GitBaseUrl.TrimEnd('/');

    private string BuildConfigurationHelpMessage(string headline) =>
        $"""
        {headline}
        Organization: '{_options.Organization}'
        Project: '{_options.Project}'
        Repository: '{_options.Repository}'
        Önerilen clone URL: {AzureDevOpsGitUrlBuilder.ToSafeDisplayUrl(AzureDevOpsGitUrlBuilder.BuildCloneUrl(_options, embedPat: false))}
        Azure DevOps → Repo → Clone → HTTPS adresini kopyalayıp AzureDevOps:CloneUrl olarak yapıştırın.
        PAT için Code (Read/Write) izni gerekir.
        """;

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(AzureDevOpsService));
        var pat = _options.PersonalAccessToken
            ?? throw new InvalidOperationException("AzureDevOps:PersonalAccessToken is not configured.");
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string BuildPullRequestDescription(string? userDescription)
    {
        if (string.IsNullOrWhiteSpace(userDescription))
            return PullRequestConstants.DeploymentNotice;

        return $"{userDescription.Trim()}\n\n---\n\n{PullRequestConstants.DeploymentNotice}";
    }

    private static string ToRef(string branch) =>
        branch.StartsWith("refs/", StringComparison.OrdinalIgnoreCase) ? branch : $"refs/heads/{branch}";

    private sealed record RepositoryResponse(string Id);
    private sealed record PullRequestResponse(int PullRequestId, string? Title);
}
