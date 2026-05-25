namespace BeDemo.Api.Models.Requests.Common;

/// <summary>Sample pagination query for P0 reference (endpoint-schema-validation).</summary>
public sealed class PaginationQuery
{
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
}
