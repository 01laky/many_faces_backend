using System.Security.Claims;
using BeDemo.Api.Models.Requests.Reels;

namespace BeDemo.Api.Services.Grid;

public interface IReelGridListService
{
	Task<object> GetReelsAsync(
		ClaimsPrincipal user,
		string? userId,
		ReelListQuery listQuery,
		CancellationToken cancellationToken = default);
}
