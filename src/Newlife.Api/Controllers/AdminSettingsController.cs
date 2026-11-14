using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Settings;
using Newlife.Application.Settings.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/admin/settings")]
[Authorize(Roles = "Admin,ContentEditor")]
public sealed class AdminSettingsController(SettingsService settingsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Dictionary<string, Dictionary<string, string>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, Dictionary<string, string>>>> List(
        CancellationToken cancellationToken)
        => Ok(await settingsService.GetAllAsync(cancellationToken));

    [HttpPut("{key}")]
    [ProducesResponseType<SettingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SettingResponse>> Update(
        string key,
        UpdateSettingRequest request,
        CancellationToken cancellationToken)
        => Ok(await settingsService.UpsertAsync(key, request, cancellationToken));
}
