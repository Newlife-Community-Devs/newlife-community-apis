using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/dev/uploads")]
public sealed class DevUploadsController(IWebHostEnvironment environment) : ControllerBase
{
    private static readonly Regex KeyPattern = new(
        @"^media/\d{4}/\d{2}/[0-9a-f]{32}\.(jpg|png|webp)$",
        RegexOptions.Compiled);

    private const long MaxBytes = 10 * 1024 * 1024;

    [HttpPut("{**key}")]
    public async Task<IActionResult> Upload(string key, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        if (!KeyPattern.IsMatch(key))
        {
            return BadRequest(new { error = "Invalid upload key." });
        }

        var root = Path.GetFullPath(Path.Combine(environment.WebRootPath, "uploads"));
        var target = Path.GetFullPath(Path.Combine(root, key));
        if (!target.StartsWith(root, StringComparison.Ordinal))
        {
            return BadRequest(new { error = "Invalid upload key." });
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        await using (var stream = System.IO.File.Create(target))
        {
            await Request.Body.CopyToAsync(stream, cancellationToken);
        }

        if (new FileInfo(target).Length > MaxBytes)
        {
            System.IO.File.Delete(target);
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "File exceeds 10 MB." });
        }

        return NoContent();
    }
}
