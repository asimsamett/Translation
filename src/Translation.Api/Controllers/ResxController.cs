using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Translation.Core.Models;
using Translation.Core.Services;

namespace Translation.Api.Controllers;

[ApiController]
[Route("api/resx")]
[Authorize]
public sealed class ResxController : ControllerBase
{
    private readonly IResxService _resx;

    public ResxController(IResxService resx) => _resx = resx;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ResxFileSummary>>> List(CancellationToken cancellationToken) =>
        Ok(await _resx.ListResxFilesAsync(cancellationToken));

    [HttpGet("{*relativePath}")]
    public async Task<ActionResult<ResxFileDetail>> Get(string relativePath, CancellationToken cancellationToken) =>
        Ok(await _resx.GetFileAsync(relativePath, cancellationToken));

    [HttpPut("{*relativePath}")]
    public async Task<ActionResult<ResxUpdateResult>> Update(
        string relativePath,
        [FromBody] ResxUpdateRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _resx.UpdateFileAsync(relativePath, request, cancellationToken));
}
