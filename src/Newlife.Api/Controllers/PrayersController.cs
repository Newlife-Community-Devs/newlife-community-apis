using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Newlife.Application.Common.Models;
using Newlife.Application.Prayers;
using Newlife.Application.Prayers.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/prayers")]
public sealed class PrayersController(PrayerService prayerService) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("prayer-submit")]
    [ProducesResponseType<PublicPrayerResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PublicPrayerResponse>> Submit(
        SubmitPrayerRequest request,
        CancellationToken cancellationToken)
        => StatusCode(
            StatusCodes.Status201Created,
            await prayerService.SubmitAsync(request, cancellationToken));

    [HttpGet("wall")]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<PublicPrayerResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PublicPrayerResponse>>> Wall(
        [FromQuery] PrayerWallQuery query,
        CancellationToken cancellationToken)
        => Ok(await prayerService.GetWallAsync(query, cancellationToken));
}
