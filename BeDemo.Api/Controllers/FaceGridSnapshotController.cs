using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Services.Grid;

namespace BeDemo.Api.Controllers;

/// <summary>
/// BE-RP8 — batch read for portal grid blocks; reuses shared grid list services (BE-RP24).
/// </summary>
[ApiController]
[Route("api/faces/{faceId:int}/grid-snapshot")]
public class FaceGridSnapshotController : ControllerBase
{
	private readonly IFaceGridSnapshotService _snapshot;

	public FaceGridSnapshotController(IFaceGridSnapshotService snapshot) => _snapshot = snapshot;

	private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

	/// <summary>
	/// GET /api/faces/{faceId}/grid-snapshot?blocks=albums,blogs,...&amp;page=1&amp;pageSize=10
	/// Unknown block keys are ignored (BE-RP8-U2); duplicate keys are deduped (BE-RP8-U4).
	/// </summary>
	[HttpGet]
	[AllowAnonymous]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetSnapshot(
		int faceId,
		[FromQuery] string? blocks,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		CancellationToken cancellationToken = default)
	{
		var parsedBlocks = GridBlockKeys.ParseBlocks(blocks);
		if (parsedBlocks.Count == 0)
		{
			return BadRequest(new
			{
				error = "At least one valid block key is required in blocks= (albums, blogs, reels, stories, chat-rooms, video-lounges, profiles, wall-tickets). Unknown keys are ignored.",
			});
		}

		if (parsedBlocks.Any(b => GridBlockKeys.AuthRequired.Contains(b)) && string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var result = await _snapshot.GetSnapshotAsync(
			faceId,
			User,
			UserId,
			parsedBlocks,
			page,
			pageSize,
			Request.Scheme,
			Request.Host.Value!,
			cancellationToken);

		return result.Status switch
		{
			FaceGridSnapshotStatus.NotFound => NotFound(new { error = "Face not found" }),
			FaceGridSnapshotStatus.Forbidden => string.IsNullOrEmpty(UserId) ? Unauthorized() : Forbid(),
			_ => Ok(result.Blocks),
		};
	}
}
