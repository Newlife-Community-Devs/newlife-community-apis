using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Common.Models;
using Newlife.Application.Events;
using Newlife.Application.Events.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/admin/events")]
[Authorize(Roles = "Admin,ContentEditor,EventManager")]
public sealed class AdminEventsController(EventService eventService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,ContentEditor,EventManager")]
    [ProducesResponseType<PagedResult<AdminEventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminEventResponse>>> List(
        [FromQuery] EventAdminQuery query,
        CancellationToken cancellationToken)
        => Ok(await eventService.ListAdminAsync(query, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin,ContentEditor")]
    [ProducesResponseType<AdminEventResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminEventResponse>> Create(
        EventUpsertRequest request,
        CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await eventService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,ContentEditor")]
    [ProducesResponseType<AdminEventResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminEventResponse>> Update(
        Guid id,
        EventUpsertRequest request,
        CancellationToken cancellationToken)
        => Ok(await eventService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,ContentEditor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await eventService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "Admin,ContentEditor")]
    [ProducesResponseType<AdminEventResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminEventResponse>> Publish(
        Guid id,
        PublishEventRequest request,
        CancellationToken cancellationToken)
        => Ok(await eventService.PublishAsync(id, request, cancellationToken));

    [HttpPost("{eventId:guid}/ticket-types")]
    [Authorize(Roles = "Admin,EventManager")]
    [ProducesResponseType<TicketTypeAdminResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketTypeAdminResponse>> CreateTicketType(
        Guid eventId,
        TicketTypeUpsertRequest request,
        CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await eventService.CreateTicketTypeAsync(eventId, request, cancellationToken));
}
