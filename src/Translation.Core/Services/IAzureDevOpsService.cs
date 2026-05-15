using Translation.Core.Models;

namespace Translation.Core.Services;

public interface IAzureDevOpsService
{
    Task EnsureRepositoryAccessibleAsync(CancellationToken cancellationToken = default);

    Task<PullRequestCreateResult> CreatePullRequestAsync(
        PullRequestCreateRequest request,
        CancellationToken cancellationToken = default);
}
