using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newlife.Application.Orders;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/payments/hubtel")]
[AllowAnonymous]
public sealed class HubtelCallbackController(
    HubtelCallbackService callbackService,
    IOptions<HubtelOptions> options) : ControllerBase
{
    [HttpPost("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        if (!TokenMatches(token))
        {
            return NotFound();
        }

        var raw = await ReadBodyAsync(cancellationToken);
        await callbackService.ProcessCheckoutCallbackAsync(
            raw, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Ok();
    }

    [HttpPost("refund-callback")]
    public async Task<IActionResult> RefundCallback(
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        if (!TokenMatches(token))
        {
            return NotFound();
        }

        var raw = await ReadBodyAsync(cancellationToken);
        await callbackService.ProcessRefundCallbackAsync(
            raw, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return Ok();
    }

    private bool TokenMatches(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(options.Value.CallbackToken);
        var provided = Encoding.UTF8.GetBytes(token);

        return expected.Length == provided.Length && CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private async Task<string> ReadBodyAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
