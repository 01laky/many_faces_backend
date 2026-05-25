using System.ComponentModel.DataAnnotations;

namespace BeDemo.Api.Models.Requests.OperatorUsers;

public sealed class OperatorBanReasonRequest
{
	[Required]
	[MinLength(10)]
	[MaxLength(2000)]
	public string Reason { get; set; } = null!;
}

public sealed class OperatorSetFaceRoleRequest
{
	[Range(1, int.MaxValue)]
	public int UserRoleId { get; set; }
}

public sealed class OperatorPlatformMessageRequest
{
	[Required]
	[MinLength(1)]
	[MaxLength(4000)]
	public string Content { get; set; } = null!;
}
