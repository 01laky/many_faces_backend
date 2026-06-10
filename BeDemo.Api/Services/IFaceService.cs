using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

public interface IFaceService
{
	Task<List<Face>> GetFacesAsync(CancellationToken cancellationToken = default);
}
