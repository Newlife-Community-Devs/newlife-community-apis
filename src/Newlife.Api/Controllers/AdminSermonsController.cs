using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Common.Models;
using Newlife.Application.Sermons;
using Newlife.Application.Sermons.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/admin/sermons")]
[Authorize(Roles = "Admin,ContentEditor")]
public sealed class AdminSermonsController(SermonService sermonService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<SermonDetailResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SermonDetailResponse>>> List(
        [FromQuery] SermonAdminQuery query,
        CancellationToken cancellationToken)
        => Ok(await sermonService.ListAdminAsync(query, cancellationToken));

    [HttpPost("sync")]
    [ProducesResponseType<SyncResultResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncResultResponse>> Sync(CancellationToken cancellationToken)
        => Ok(await sermonService.SyncAsync(cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<SermonDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SermonDetailResponse>> Update(
        Guid id,
        UpdateSermonVisibilityRequest request,
        CancellationToken cancellationToken)
        => Ok(await sermonService.UpdateVisibilityAsync(id, request, cancellationToken));
}
