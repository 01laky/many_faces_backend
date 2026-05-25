namespace BeDemo.Api.Models.Requests.Admin;

public sealed class UpdateAdminPushSettingsRequest
{
	public bool Enabled { get; set; }

	public string? WorkerGrpcUrl { get; set; }

	/// <summary>Omit to keep; empty string clears.</summary>
	public string? WorkerAuthToken { get; set; }

	public UpdateAdminPushFirebaseRequest? Firebase { get; set; }

	public UpdateAdminPushDefaultsRequest? Defaults { get; set; }

	public int? GrpcDeadlineSeconds { get; set; }
}

public sealed class UpdateAdminPushFirebaseRequest
{
	/// <summary>Omit to keep; empty string clears.</summary>
	public string? ServiceAccountJson { get; set; }
}

public sealed class UpdateAdminPushDefaultsRequest
{
	public string? TitleLocKey { get; set; }

	public string? BodyLocKey { get; set; }

	public string? AndroidChannelId { get; set; }
}

public sealed class TestAdminPushFcmRequest
{
	public UpdateAdminPushFirebaseRequest? Firebase { get; set; }
}
