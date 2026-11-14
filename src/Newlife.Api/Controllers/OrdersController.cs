using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Orders;
using Newlife.Application.Orders.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
public sealed class OrdersController(OrderService orderService) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("orders")]
    [ProducesResponseType<OrderCreatedResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderCreatedResponse>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await orderService.CreateAsync(request, cancellationToken));

    [HttpGet("{orderNumber}")]
    [AllowAnonymous]
    [ProducesResponseType<OrderReadResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderReadResponse>> Read(
        string orderNumber,
        [FromQuery] string? token,
        CancellationToken cancellationToken)
        => Ok(await orderService.ReadAsync(orderNumber, ParseToken(token), cancellationToken));

    [HttpPost("{orderNumber}/retry-payment")]
    [AllowAnonymous]
    [EnableRateLimiting("orders")]
    [ProducesResponseType<RetryPaymentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RetryPaymentResponse>> RetryPayment(
        string orderNumber,
        [FromQuery] string? token,
        CancellationToken cancellationToken)
        => Ok(await orderService.RetryPaymentAsync(orderNumber, ParseToken(token), cancellationToken));

    private static Guid ParseToken(string? token)
        => Guid.TryParse(token, out var parsed)
            ? parsed
            : throw new NotFoundException("order.not_found", "Order was not found.");
}
