using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Announcements;
using Newlife.Application.Announcements.DTOs;
using Newlife.Application.Common.Models;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/announcements")]
public sealed class AnnouncementsController(AnnouncementService announcementService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<AnnouncementResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AnnouncementResponse>>> List(
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken)
        => Ok(await announcementService.ListPublicAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<AnnouncementResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AnnouncementResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await announcementService.GetByIdAsync(id, cancellationToken));
}
