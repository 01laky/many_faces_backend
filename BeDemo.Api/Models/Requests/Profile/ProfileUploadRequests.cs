using Microsoft.AspNetCore.Http;

namespace BeDemo.Api.Models.Requests.Profile;

public sealed class AvatarUploadRequest
{
	public IFormFile? File { get; set; }
}

public sealed class FaceAvatarUploadRequest
{
	public IFormFile? File { get; set; }
	public int FaceId { get; set; }
}
