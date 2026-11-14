using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Api.Extensions;
using Newlife.Application.Orders;
using Newlife.Application.Orders.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/admin/check-in")]
[Authorize(Roles = "Admin,EventManager")]
public sealed class AdminCheckInController(CheckInService checkInService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CheckInResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckInResponse>> CheckIn(
        CheckInRequest request,
        CancellationToken cancellationToken)
        => Ok(await checkInService.CheckInAsync(request.Code, User.GetUserIdOrNull(), cancellationToken));

    [HttpGet("stats")]
    [ProducesResponseType<CheckInStatsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckInStatsResponse>> Stats(
        [FromQuery] Guid? eventId,
        CancellationToken cancellationToken)
        => Ok(await checkInService.GetStatsAsync(eventId, cancellationToken));
}
