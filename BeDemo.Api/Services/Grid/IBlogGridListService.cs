using System.Security.Claims;
using BeDemo.Api.Models.Requests.Blogs;

namespace BeDemo.Api.Services.Grid;

public interface IBlogGridListService
{
	Task<object> GetBlogsAsync(
		ClaimsPrincipal user,
		string? userId,
		BlogListQuery listQuery,
		CancellationToken cancellationToken = default);
}
