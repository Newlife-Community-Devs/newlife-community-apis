using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Common.Models;
using Newlife.Application.Orders;
using Newlife.Application.Orders.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/admin/orders")]
[Authorize(Roles = "Admin,EventManager")]
public sealed class AdminOrdersController(AdminOrderService adminOrderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AdminOrderListResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminOrderListResponse>>> List(
        [FromQuery] AdminOrderQuery query,
        CancellationToken cancellationToken)
        => Ok(await adminOrderService.ListAsync(query, cancellationToken));

    [HttpGet("{orderNumber}")]
    [ProducesResponseType<AdminOrderResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminOrderResponse>> Get(
        string orderNumber,
        CancellationToken cancellationToken)
        => Ok(await adminOrderService.GetDetailAsync(orderNumber, cancellationToken));

    [HttpPost("{orderNumber}/refund")]
    [ProducesResponseType<AdminOrderResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminOrderResponse>> Refund(
        string orderNumber,
        CancellationToken cancellationToken)
        => Ok(await adminOrderService.RefundAsync(orderNumber, cancellationToken));

    [HttpPost("{orderNumber}/mark-reviewed")]
    [ProducesResponseType<AdminOrderResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminOrderResponse>> MarkReviewed(
        string orderNumber,
        CancellationToken cancellationToken)
        => Ok(await adminOrderService.MarkReviewedAsync(orderNumber, cancellationToken));
}
