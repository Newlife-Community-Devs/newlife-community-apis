using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Common.Models;
using Newlife.Application.Sermons;
using Newlife.Application.Sermons.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/sermons")]
public sealed class SermonsController(SermonService sermonService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<SermonListResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SermonListResponse>>> List(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken)
        => Ok(await sermonService.ListPublicAsync(query, cancellationToken));

    [HttpGet("latest")]
    [AllowAnonymous]
    [ProducesResponseType<LatestSermonResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LatestSermonResponse>> Latest(CancellationToken cancellationToken)
        => Ok(await sermonService.GetLatestAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<SermonDetailResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SermonDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await sermonService.GetAsync(id, cancellationToken));
}
