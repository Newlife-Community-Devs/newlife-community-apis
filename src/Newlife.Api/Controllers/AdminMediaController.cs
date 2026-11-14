using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newlife.Application.Common.Models;
using Newlife.Application.Media;
using Newlife.Application.Media.DTOs;

namespace Newlife.Api.Controllers;

[ApiController]
[Route("api/v1/admin/media")]
[Authorize(Roles = "Admin,ContentEditor")]
public sealed class AdminMediaController(MediaService mediaService) : ControllerBase
{
    [HttpPost("uploads")]
    [ProducesResponseType<PresignedUpload>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PresignedUpload>> CreateUpload(
        CreateUploadRequest request,
        CancellationToken cancellationToken)
        => Ok(await mediaService.CreatePresignedUploadAsync(request, cancellationToken));

    [HttpPost]
    [ProducesResponseType<MediaAssetResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MediaAssetResponse>> Register(
        RegisterMediaRequest request,
        CancellationToken cancellationToken)
        => StatusCode(
            StatusCodes.Status201Created,
            await mediaService.RegisterAsync(request, cancellationToken));

    [HttpGet]
    [ProducesResponseType<PagedResult<MediaAssetResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MediaAssetResponse>>> List(
        [FromQuery] MediaQuery query,
        CancellationToken cancellationToken)
        => Ok(await mediaService.ListAsync(query, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediaService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
