using System.Security.Claims;
using BeDemo.Api.Models.Requests.Stories;

namespace BeDemo.Api.Services.Grid;

public interface IStoryGridListService
{
	Task<object> GetStoriesAsync(
		ClaimsPrincipal user,
		string? userId,
		StoryListQuery listQuery,
		CancellationToken cancellationToken = default);
}
