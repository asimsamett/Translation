using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Translation.Core.Models;
using Translation.Core.Options;

namespace Translation.Core.Services;

public sealed class GitWorkspaceService : IGitWorkspaceService
{
    private readonly AzureDevOpsOptions _devOps;
    private readonly WorkspaceOptions _workspace;
    private readonly BranchNamingService _branchNaming;
    private readonly IAzureDevOpsService _azureDevOps;
    private readonly ILogger<GitWorkspaceService> _logger;

    public GitWorkspaceService(
        IOptions<AzureDevOpsOptions> devOps,
        IOptions<WorkspaceOptions> workspace,
        BranchNamingService branchNaming,
        IAzureDevOpsService azureDevOps,
        ILogger<GitWorkspaceService> logger)
    {
        _devOps = devOps.Value;
        _workspace = workspace.Value;
        _branchNaming = branchNaming;
        _azureDevOps = azureDevOps;
        _logger = logger;
    }

    public async Task<string> EnsureRepositoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var localPath = GetLocalRepositoryPath();

        if (!Repository.IsValid(localPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            await _azureDevOps.EnsureRepositoryAccessibleAsync(cancellationToken);

            var cloneUrl = AzureDevOpsGitUrlBuilder.BuildCloneUrl(_devOps, _devOps.EmbedPatInCloneUrl);
            _logger.LogInformation(
                "Cloning {Repo} from {Url} into {Path}",
                _devOps.Repository,
                AzureDevOpsGitUrlBuilder.ToSafeDisplayUrl(cloneUrl),
                localPath);

            try
            {
                var cloneOptions = new CloneOptions();
                cloneOptions.FetchOptions.CredentialsProvider = CreateCredentials;
                Repository.Clone(cloneUrl, localPath, cloneOptions);
            }
            catch (LibGit2SharpException ex) when (ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"""
                    Git clone başarısız (404). Repo veya URL hatalı olabilir.
                    Denenen URL: {AzureDevOpsGitUrlBuilder.ToSafeDisplayUrl(cloneUrl)}
                    Organization='{_devOps.Organization}', Project='{_devOps.Project}', Repository='{_devOps.Repository}'
                    Azure DevOps → Clone → HTTPS URL'yi AzureDevOps:CloneUrl olarak ayarlayın.
                    """,
                    ex);
            }
        }

        return localPath;
    }

    public async Task<GitSyncResult> PullAsync(CancellationToken cancellationToken = default)
    {
        var localPath = await EnsureRepositoryAsync(cancellationToken);
        using var repo = new Repository(localPath);
        Fetch(repo);

        var branchName = repo.Head.FriendlyName;
        var signature = CreateSignature();
        var pullOptions = new PullOptions
        {
            FetchOptions = new FetchOptions { CredentialsProvider = CreateCredentials },
            MergeOptions = new MergeOptions { FastForwardStrategy = FastForwardStrategy.Default }
        };

        var result = Commands.Pull(repo, signature, pullOptions);
        var message = result.Status switch
        {
            MergeStatus.UpToDate => "Repository is up to date.",
            MergeStatus.FastForward => "Fast-forward pull completed.",
            _ => $"Pull completed with status {result.Status}."
        };

        _logger.LogInformation("Pulled {Branch}: {Message}", branchName, message);
        return new GitSyncResult(branchName, 0, message);
    }

    public async Task<BranchListResult> ListBranchesAsync(CancellationToken cancellationToken = default)
    {
        var localPath = await EnsureRepositoryAsync(cancellationToken);
        using var repo = new Repository(localPath);
        Fetch(repo);

        var branches = repo.Branches
            .Where(b => b.IsRemote && b.FriendlyName.StartsWith("origin/", StringComparison.Ordinal))
            .Select(b => b.FriendlyName["origin/".Length..])
            .Where(n => !string.Equals(n, "HEAD", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BranchListResult(branches, repo.Head.FriendlyName);
    }

    public async Task<BranchDefaultsResult> GetBranchDefaultsAsync(
        string? userName,
        string? suffix = null,
        CancellationToken cancellationToken = default)
    {
        var list = await ListBranchesAsync(cancellationToken);
        var suggested = _branchNaming.Suggest(userName, suffix);

        if (list.Branches.Any(b => string.Equals(b, suggested, StringComparison.OrdinalIgnoreCase)))
            suggested = _branchNaming.Suggest(userName, $"{suffix}-{_devOps.Repository}");

        var translationBranches = list.Branches
            .Where(b => b.StartsWith("translation/", StringComparison.OrdinalIgnoreCase))
            .Take(15)
            .ToList();

        return new BranchDefaultsResult(
            suggested,
            _devOps.DefaultBranch,
            list.CurrentBranch,
            translationBranches.Count > 0 ? translationBranches : list.Branches.Take(20).ToList());
    }

    public async Task<CreateBranchResult> CreateBranchAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        _branchNaming.ValidateOrThrow(request.BranchName);

        var localPath = await EnsureRepositoryAsync(cancellationToken);
        using var repo = new Repository(localPath);
        Fetch(repo);

        var basedOn = request.FromBranch ?? _devOps.DefaultBranch;
        CheckoutBaseBranch(repo, basedOn);

        var signature = CreateSignature();
        Commands.Pull(repo, signature, new PullOptions
        {
            FetchOptions = new FetchOptions { CredentialsProvider = CreateCredentials }
        });

        var branchName = request.BranchName.Trim();
        var existing = repo.Branches[branchName];
        if (existing is not null)
        {
            Commands.Checkout(repo, existing);
            return new CreateBranchResult(branchName, basedOn, false, $"Branch zaten var; '{branchName}' checkout edildi.");
        }

        var remote = repo.Branches[$"origin/{branchName}"];
        if (remote is not null)
        {
            var tracking = repo.CreateBranch(branchName, remote.Tip);
            repo.Branches.Update(tracking, b => b.Remote = "origin", b => b.UpstreamBranch = branchName);
            Commands.Checkout(repo, tracking);
            return new CreateBranchResult(branchName, basedOn, false, $"Uzak branch bulundu; '{branchName}' checkout edildi.");
        }

        var created = repo.CreateBranch(branchName);
        Commands.Checkout(repo, created);

        _logger.LogInformation("Created branch {Branch} from {Base}", branchName, basedOn);
        return new CreateBranchResult(branchName, basedOn, true, $"Yeni branch oluşturuldu: {branchName}");
    }

    public async Task<GitCommitResult> CommitAndPushAsync(
        GitCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        _branchNaming.ValidateOrThrow(request.BranchName);

        var localPath = await EnsureRepositoryAsync(cancellationToken);
        using var repo = new Repository(localPath);
        Fetch(repo);

        EnsureOnBranch(repo, request.BranchName);

        var signature = CreateSignature();
        Commands.Pull(repo, signature, new PullOptions
        {
            FetchOptions = new FetchOptions { CredentialsProvider = CreateCredentials }
        });

        var pathsToStage = request.ResxRelativePaths
            .Select(NormalizeRelative)
            .Select(p => Path.Combine(localPath, p.Replace('/', Path.DirectorySeparatorChar)))
            .Where(File.Exists)
            .ToList();

        if (pathsToStage.Count == 0)
            throw new InvalidOperationException("No resource files to commit.");

        Commands.Stage(repo, pathsToStage);

        var commit = repo.Commit(request.CommitMessage, signature, signature);
        var pushOptions = new PushOptions { CredentialsProvider = CreateCredentials };
        repo.Network.Push(repo.Head, pushOptions);

        _logger.LogInformation("Pushed commit {Sha} on {Branch}", commit.Sha, request.BranchName);
        return new GitCommitResult(request.BranchName, commit.Sha, pathsToStage.Count);
    }

    private void EnsureOnBranch(Repository repo, string branchName)
    {
        var local = repo.Branches[branchName];
        if (local is not null)
        {
            Commands.Checkout(repo, local);
            return;
        }

        var remote = repo.Branches[$"origin/{branchName}"];
        if (remote is not null)
        {
            var tracking = repo.CreateBranch(branchName, remote.Tip);
            repo.Branches.Update(tracking, b => b.Remote = "origin", b => b.UpstreamBranch = branchName);
            Commands.Checkout(repo, tracking);
            return;
        }

        CheckoutBaseBranch(repo, _devOps.DefaultBranch);
        var created = repo.CreateBranch(branchName);
        Commands.Checkout(repo, created);
    }

    private void CheckoutBaseBranch(Repository repo, string baseBranchName)
    {
        var local = repo.Branches[baseBranchName];
        if (local is not null)
        {
            Commands.Checkout(repo, local);
            return;
        }

        var remote = repo.Branches[$"origin/{baseBranchName}"]
            ?? throw new InvalidOperationException($"Base branch not found: {baseBranchName}");

        var tracking = repo.CreateBranch(baseBranchName, remote.Tip);
        repo.Branches.Update(tracking, b => b.Remote = "origin", b => b.UpstreamBranch = baseBranchName);
        Commands.Checkout(repo, tracking);
    }

    private void Fetch(Repository repo)
    {
        var remote = repo.Network.Remotes["origin"]
            ?? throw new InvalidOperationException("Remote 'origin' not found.");
        Commands.Fetch(repo, remote.Name, Array.Empty<string>(), new FetchOptions { CredentialsProvider = CreateCredentials }, null);
    }

    private string GetLocalRepositoryPath() =>
        Path.Combine(_workspace.RootPath, _devOps.Organization, _devOps.Project, _devOps.Repository);

    private Credentials CreateCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types) =>
        new UsernamePasswordCredentials
        {
            Username = string.IsNullOrWhiteSpace(_devOps.GitUsername) ? "pat" : _devOps.GitUsername,
            Password = GetPat()
        };

    private Signature CreateSignature() => new("Translation Tool", "translation-tool@local", DateTimeOffset.Now);

    private string GetPat()
    {
        if (string.IsNullOrWhiteSpace(_devOps.PersonalAccessToken))
            throw new InvalidOperationException("AzureDevOps:PersonalAccessToken is not configured.");
        return _devOps.PersonalAccessToken;
    }

    private static string NormalizeRelative(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}
