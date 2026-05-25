using System.ComponentModel.DataAnnotations;

namespace BeDemo.Api.Models.Requests.OperatorContent;

/// <summary>Super-admin hard-delete album or delete media with audit + optional creator DM.</summary>
public sealed class OperatorAlbumDeleteRequest
{
	[Range(1, int.MaxValue)]
	public int FaceId { get; set; }

	[Required]
	[MinLength(10)]
	[MaxLength(2000)]
	public string Reason { get; set; } = null!;

	[Required]
	[MinLength(10)]
	[MaxLength(2000)]
	public string UserMessage { get; set; } = null!;
}
