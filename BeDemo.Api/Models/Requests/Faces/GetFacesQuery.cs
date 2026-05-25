namespace BeDemo.Api.Models.Requests.Faces;

/// <summary>GET /api/faces list query (admin table server pagination/sort/filter).</summary>
public sealed class GetFacesQuery
{
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	public string? Search { get; set; }
	public string? SortBy { get; set; }
	public string? SortDir { get; set; }
	public string? Visibility { get; set; }
	public bool? IsPublic { get; set; }
}
