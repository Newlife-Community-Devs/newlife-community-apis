using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Common.Models;
using Newlife.Application.Events;
using Newlife.Application.Events.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/events")]
public sealed class EventsController(EventService eventService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<PublicEventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PublicEventResponse>>> List(
        [FromQuery] EventPublicQuery query,
        CancellationToken cancellationToken)
        => Ok(await eventService.ListPublicAsync(query, cancellationToken));

    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType<PublicEventResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicEventResponse>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
        => Ok(await eventService.GetBySlugAsync(slug, cancellationToken));

    [HttpGet("{slug}/ticket-types")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<PublicTicketTypeResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicTicketTypeResponse>>> TicketTypes(
        string slug,
        CancellationToken cancellationToken)
        => Ok(await eventService.GetPublicTicketTypesAsync(slug, cancellationToken));
}
