using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Events;
using Newlife.Application.Events.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/admin/ticket-types")]
[Authorize(Roles = "Admin,EventManager")]
public sealed class AdminTicketTypesController(EventService eventService) : ControllerBase
{
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TicketTypeAdminResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketTypeAdminResponse>> Update(
        Guid id,
        TicketTypeUpsertRequest request,
        CancellationToken cancellationToken)
        => Ok(await eventService.UpdateTicketTypeAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await eventService.DeleteTicketTypeAsync(id, cancellationToken);
        return NoContent();
    }
}
