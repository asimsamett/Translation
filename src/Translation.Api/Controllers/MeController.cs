using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Translation.Api.Controllers;

[ApiController]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult Get() =>
        Ok(new
        {
            name = User.Identity?.Name,
            isAuthenticated = User.Identity?.IsAuthenticated ?? false
        });
}
