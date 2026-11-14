using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Settings;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/settings")]
public sealed class SettingsController(SettingsService settingsService) : ControllerBase
{
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType<Dictionary<string, string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, string>>> Public(CancellationToken cancellationToken)
        => Ok(await settingsService.GetPublicAsync(cancellationToken));
}
