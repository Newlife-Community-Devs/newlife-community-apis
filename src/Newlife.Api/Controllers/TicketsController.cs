using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using Newlife.Application.Orders;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/tickets")]
public sealed class TicketsController(TicketQrService ticketQrService) : ControllerBase
{
    [HttpGet("{code}/qr.png")]
    [AllowAnonymous]
    [Produces("image/png")]
    public async Task<IActionResult> QrImage(
        string code,
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(token, out var orderToken))
        {
            return NotFound();
        }

        var ticketCode = await ticketQrService.GetCodeAsync(code, orderToken, cancellationToken);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(ticketCode, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);

        return File(png.GetGraphic(10, [0x00, 0x1F, 0x3F], [0xFF, 0xFF, 0xFF]), "image/png");
    }
}
