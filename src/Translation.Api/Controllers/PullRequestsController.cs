using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Translation.Core.Models;
using Translation.Core.Options;
using Translation.Core.Services;

namespace Translation.Api.Controllers;

[ApiController]
[Route("api/pull-requests")]
[Authorize]
public sealed class PullRequestsController : ControllerBase
{
    private readonly IAzureDevOpsService _devOps;
    private readonly AzureDevOpsOptions _options;

    public PullRequestsController(IAzureDevOpsService devOps, IOptions<AzureDevOpsOptions> options)
    {
        _devOps = devOps;
        _options = options.Value;
    }

    [HttpPost]
    public async Task<ActionResult<PullRequestCreateResult>> Create(
        [FromBody] PullRequestCreateRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _devOps.CreatePullRequestAsync(request, cancellationToken));

    [HttpGet("defaults")]
    public ActionResult<object> Defaults() =>
        Ok(new { targetBranch = _options.DefaultBranch });
}
