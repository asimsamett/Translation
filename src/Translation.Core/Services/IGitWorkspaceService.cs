using Translation.Core.Models;

namespace Translation.Core.Services;

public interface IGitWorkspaceService
{
    Task<string> EnsureRepositoryAsync(CancellationToken cancellationToken = default);
    Task<GitSyncResult> PullAsync(CancellationToken cancellationToken = default);
    Task<BranchListResult> ListBranchesAsync(CancellationToken cancellationToken = default);
    Task<BranchDefaultsResult> GetBranchDefaultsAsync(string? userName, string? suffix = null, CancellationToken cancellationToken = default);
    Task<CreateBranchResult> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task<GitCommitResult> CommitAndPushAsync(GitCommitRequest request, CancellationToken cancellationToken = default);
}
