namespace BeDemo.Api.Models.DTOs;

/// <summary>GET /api/me/capabilities — FE should drive navigation from <see cref="Permissions"/> (A10).</summary>
public class CapabilitiesResponse
{
	public string GlobalRole { get; set; } = string.Empty;
	public int RequestFaceId { get; set; }
	public string? RequestFaceIndex { get; set; }
	public bool IsAdminFaceScope { get; set; }
	public string? MyFaceRoleName { get; set; }
	public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}
