using System.Security.Claims;
using BeDemo.Api.Models.Requests.OperatorContent;
using BeDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeDemo.Api.Controllers;

/// <summary>Super-admin operator content actions (album/reel/blog hard-delete shared by Remove + detail delete UI).</summary>
[ApiController]
[Route("api/operator-content")]
[Authorize]
public sealed class OperatorContentController : ControllerBase
{
    private readonly IAccessEvaluator _access;
    private readonly IOperatorAlbumManagementService _albums;
    private readonly IOperatorReelManagementService _reels;
    private readonly IOperatorBlogManagementService _blogs;
    private readonly IOperatorChatRoomManagementService _chatRooms;
    private readonly IOperatorProfileSocialManagementService _profiles;

    public OperatorContentController(
        IAccessEvaluator access,
        IOperatorAlbumManagementService albums,
        IOperatorReelManagementService reels,
        IOperatorBlogManagementService blogs,
        IOperatorChatRoomManagementService chatRooms,
        IOperatorProfileSocialManagementService profiles)
    {
        _access = access;
        _albums = albums;
        _reels = reels;
        _blogs = blogs;
        _chatRooms = chatRooms;
        _profiles = profiles;
    }

    private string? OperatorUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private bool RequireSuperAdmin() => _access.IsGlobalSuperAdmin(User);

    /// <summary>Hard-delete album (toolbar Remove and Delete album both use this).</summary>
    [HttpPost("albums/{id:int}/delete")]
    public async Task<IActionResult> HardDeleteAlbum(
        int id,
        [FromBody] OperatorAlbumDeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireSuperAdmin())
            return Forbid();
        if (string.IsNullOrEmpty(OperatorUserId))
            return Unauthorized();

        await _albums.HardDeleteAlbumAsync(
            OperatorUserId,
            id,
            request.FaceId,
            request.Reason,
            request.UserMessage,
            cancellationToken);

        return NoContent();
    }

    /// <summary>Delete one album media item; album row remains.</summary>
    [HttpPost("albums/{albumId:int}/media/{mediaId:int}/delete")]
    public async Task<IActionResult> DeleteAlbumMedia(
        int albumId,
        int mediaId,
        [FromBody] OperatorAlbumDeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireSuperAdmin())
            return Forbid();
        if (string.IsNullOrEmpty(OperatorUserId))
            return Unauthorized();

        var ok = await _albums.DeleteAlbumMediaAsync(
            OperatorUserId,
            albumId,
            mediaId,
            request.FaceId,
            request.Reason,
            request.UserMessage,
            cancellationToken);

        return ok ? NoContent() : NotFound(new { error = "Album or media not found" });
    }

    /// <summary>Hard-delete reel (toolbar Remove and Delete reel both use this).</summary>
    [HttpPost("reels/{id:int}/delete")]
    public async Task<IActionResult> HardDeleteReel(
        int id,
        [FromBody] OperatorAlbumDeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireSuperAdmin())
            return Forbid();
        if (string.IsNullOrEmpty(OperatorUserId))
            return Unauthorized();

        await _reels.HardDeleteReelAsync(
            OperatorUserId,
            id,
            request.FaceId,
            request.Reason,
            request.UserMessage,
            cancellationToken);

        return NoContent();
    }

    /// <summary>Hard-delete blog (toolbar Remove and Delete blog both use this).</summary>
    [HttpPost("blogs/{id:int}/delete")]
    public async Task<IActionResult> HardDeleteBlog(
        int id,
        [FromBody] OperatorAlbumDeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireSuperAdmin())
            return Forbid();
        if (string.IsNullOrEmpty(OperatorUserId))
            return Unauthorized();

        await _blogs.HardDeleteBlogAsync(
            OperatorUserId,
            id,
            request.FaceId,
            request.Reason,
            request.UserMessage,
            cancellationToken);

        return NoContent();
    }

    /// <summary>Delete one blog image; blog row remains.</summary>
    [HttpPost("blogs/{blogId:int}/images/{imageId:int}/delete")]
    public async Task<IActionResult> DeleteBlogImage(
        int blogId,
        int imageId,
        [FromBody] OperatorAlbumDeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireSuperAdmin())
            return Forbid();
        if (string.IsNullOrEmpty(OperatorUserId))
            return Unauthorized();

        var ok = await _blogs.DeleteBlogImageAsync(
            OperatorUserId,
            blogId,
            imageId,
            request.FaceId,
            request.Reason,
            request.UserMessage,
            cancellationToken);

        return ok ? NoContent() : NotFound(new { error = "Blog or image not found" });
    }

    /// <summary>Hard-delete face chat room (operator detail Delete room).</summary>
    [HttpPost("chat-rooms/{roomId:int}/delete")]
    public async Task<IActionResult> HardDeleteChatRoom(
        int roomId,
        [FromBody] OperatorAlbumDeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireSuperAdmin())
            return Forbid();
        if (string.IsNullOrEmpty(OperatorUserId))
            return Unauthorized();

        await _chatRooms.HardDeleteRoomAsync(
            OperatorUserId,
            roomId,
            request.FaceId,
            request.Reason,
            request.UserMessage,
            cancellationToken);

        return NoContent();
    }

    /// <summary>Remove one profile comment (operator profile detail row delete).</summary>
    [HttpPost("profile-comments/{commentId:int}/delete")]
    public async Task<IActionResult> DeleteProfileComment(
        int commentId,
        [FromBody] OperatorAlbumDeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireSuperAdmin())
            return Forbid();
        if (string.IsNullOrEmpty(OperatorUserId))
            return Unauthorized();

        var ok = await _profiles.DeleteCommentAsync(
            OperatorUserId,
            commentId,
            request.FaceId,
            request.Reason,
            request.UserMessage,
            cancellationToken);
        return ok ? NoContent() : NotFound(new { error = "Comment not found" });
    }

    /// <summary>Remove one profile review (operator profile detail row delete).</summary>
    [HttpPost("profile-reviews/{reviewId:int}/delete")]
    public async Task<IActionResult> DeleteProfileReview(
        int reviewId,
        [FromBody] OperatorAlbumDeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireSuperAdmin())
            return Forbid();
        if (string.IsNullOrEmpty(OperatorUserId))
            return Unauthorized();

        var ok = await _profiles.DeleteReviewAsync(
            OperatorUserId,
            reviewId,
            request.FaceId,
            request.Reason,
            request.UserMessage,
            cancellationToken);
        return ok ? NoContent() : NotFound(new { error = "Review not found" });
    }
}
