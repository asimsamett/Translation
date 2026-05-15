using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Translation.Core.Models;
using Translation.Core.Options;
using Translation.Core.Services;

namespace Translation.Api.Controllers;

[ApiController]
[Route("api/git")]
[Authorize]
public sealed class GitController : ControllerBase
{
    private readonly IGitWorkspaceService _git;
    private readonly IAzureDevOpsService _azureDevOps;
    private readonly AzureDevOpsOptions _devOpsOptions;

    public GitController(
        IGitWorkspaceService git,
        IAzureDevOpsService azureDevOps,
        IOptions<AzureDevOpsOptions> devOpsOptions)
    {
        _git = git;
        _azureDevOps = azureDevOps;
        _devOpsOptions = devOpsOptions.Value;
    }

    [HttpGet("connection-check")]
    public async Task<ActionResult<object>> ConnectionCheck(CancellationToken cancellationToken)
    {
        await _azureDevOps.EnsureRepositoryAccessibleAsync(cancellationToken);
        var cloneUrl = AzureDevOpsGitUrlBuilder.BuildCloneUrl(_devOpsOptions, embedPat: false);
        return Ok(new
        {
            ok = true,
            organization = _devOpsOptions.Organization,
            project = _devOpsOptions.Project,
            repository = _devOpsOptions.Repository,
            cloneUrl = AzureDevOpsGitUrlBuilder.ToSafeDisplayUrl(cloneUrl),
            usingCloneUrlOverride = !string.IsNullOrWhiteSpace(_devOpsOptions.CloneUrl)
        });
    }

    [HttpPost("pull")]
    public async Task<ActionResult<GitSyncResult>> Pull(CancellationToken cancellationToken) =>
        Ok(await _git.PullAsync(cancellationToken));

    [HttpGet("branches")]
    public async Task<ActionResult<BranchListResult>> ListBranches(CancellationToken cancellationToken) =>
        Ok(await _git.ListBranchesAsync(cancellationToken));

    [HttpGet("branch-defaults")]
    public async Task<ActionResult<BranchDefaultsResult>> BranchDefaults(
        [FromQuery] string? suffix,
        CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name;
        return Ok(await _git.GetBranchDefaultsAsync(userName, suffix, cancellationToken));
    }

    [HttpPost("branches")]
    public async Task<ActionResult<CreateBranchResult>> CreateBranch(
        [FromBody] CreateBranchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _git.CreateBranchAsync(request, cancellationToken));

    [HttpPost("commit-push")]
    public async Task<ActionResult<GitCommitResult>> CommitAndPush(
        [FromBody] GitCommitRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _git.CommitAndPushAsync(request, cancellationToken));
}
