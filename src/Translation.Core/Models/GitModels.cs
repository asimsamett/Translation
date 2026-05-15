namespace Translation.Core.Models;

public sealed record GitSyncResult(string Branch, int CommitsPulled, string Message);

public sealed record GitCommitRequest(string BranchName, string CommitMessage, IReadOnlyList<string> ResxRelativePaths);

public sealed record GitCommitResult(string Branch, string CommitSha, int FilesCommitted);

public sealed record PullRequestCreateRequest(
    string SourceBranch,
    string TargetBranch,
    string Title,
    string? Description);

public sealed record PullRequestCreateResult(int PullRequestId, string Url, string Title);

public sealed record BranchListResult(IReadOnlyList<string> Branches, string? CurrentBranch);

public sealed record BranchDefaultsResult(
    string SuggestedBranchName,
    string TargetBranch,
    string? CurrentBranch,
    IReadOnlyList<string> Branches);

public sealed record CreateBranchRequest(string BranchName, string? FromBranch = null);

public sealed record CreateBranchResult(string BranchName, string BasedOn, bool Created, string Message);
