using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Common.Models;
using Newlife.Application.Prayers;
using Newlife.Application.Prayers.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/admin/prayers")]
[Authorize(Roles = "Admin,ContentEditor")]
public sealed class AdminPrayersController(PrayerService prayerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AdminPrayerResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminPrayerResponse>>> List(
        [FromQuery] PrayerAdminQuery query,
        CancellationToken cancellationToken)
        => Ok(await prayerService.ListAsync(query, cancellationToken));

    [HttpGet("stats")]
    [ProducesResponseType<PrayerStatsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PrayerStatsResponse>> Stats(CancellationToken cancellationToken)
        => Ok(await prayerService.GetStatsAsync(cancellationToken));

    [HttpPost("{id:guid}/answer")]
    [ProducesResponseType<AdminPrayerResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminPrayerResponse>> Answer(
        Guid id,
        AnswerPrayerRequest request,
        CancellationToken cancellationToken)
        => Ok(await prayerService.AnswerAsync(id, cancellationToken));

    [HttpPost("{id:guid}/flag")]
    [ProducesResponseType<AdminPrayerResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminPrayerResponse>> Flag(
        Guid id,
        FlagPrayerRequest request,
        CancellationToken cancellationToken)
        => Ok(await prayerService.FlagAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/unflag")]
    [ProducesResponseType<AdminPrayerResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminPrayerResponse>> Unflag(Guid id, CancellationToken cancellationToken)
        => Ok(await prayerService.UnflagAsync(id, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await prayerService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
