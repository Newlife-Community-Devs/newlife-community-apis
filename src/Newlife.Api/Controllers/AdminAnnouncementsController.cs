using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Announcements;
using Newlife.Application.Announcements.DTOs;
using Newlife.Application.Common.Models;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/admin/announcements")]
[Authorize(Roles = "Admin,ContentEditor")]
public sealed class AdminAnnouncementsController(AnnouncementService announcementService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AnnouncementResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AnnouncementResponse>>> List(
        [FromQuery] AnnouncementAdminQuery query,
        CancellationToken cancellationToken)
        => Ok(await announcementService.ListAdminAsync(query, cancellationToken));

    [HttpPost]
    [ProducesResponseType<AnnouncementResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AnnouncementResponse>> Create(
        AnnouncementUpsertRequest request,
        CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await announcementService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AnnouncementResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AnnouncementResponse>> Update(
        Guid id,
        AnnouncementUpsertRequest request,
        CancellationToken cancellationToken)
        => Ok(await announcementService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await announcementService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
