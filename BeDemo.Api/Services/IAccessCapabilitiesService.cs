using System.Security.Claims;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Services;

public interface IAccessCapabilitiesService
{
	Task<CapabilitiesResponse> GetCapabilitiesAsync(string userId, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
