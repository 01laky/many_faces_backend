using System.Security.Claims;
using BeDemo.Api.Models.Requests.Albums;

namespace BeDemo.Api.Services.Grid;

public interface IAlbumGridListService
{
	Task<object> GetAlbumsAsync(
		ClaimsPrincipal user,
		string? userId,
		AlbumListQuery listQuery,
		CancellationToken cancellationToken = default);
}
