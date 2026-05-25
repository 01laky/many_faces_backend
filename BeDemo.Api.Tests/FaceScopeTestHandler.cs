using System.Net.Http;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Tests;

/// <summary>
/// Prepends <c>/{face-kebab}</c> to API and SignalR paths so integration tests can keep calling
/// <c>/api/...</c> while the host expects <c>/{face}/api/...</c>. Exempt paths match <see cref="Routing.IsExemptFromFaceScope"/>.
/// </summary>
public sealed class FaceScopeTestHandler : DelegatingHandler
{
	private readonly string _prefix;

	/// <param name="faceIndex">Logical face index (e.g. <c>public</c>, <c>admin</c>); converted with <see cref="Routing.ConvertToKebabCase"/>.</param>
	public FaceScopeTestHandler(string faceIndex)
	{
		var kebab = Routing.ConvertToKebabCase(faceIndex).Trim('/');
		_prefix = string.IsNullOrEmpty(kebab) ? "/public" : "/" + kebab;
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		if (request.RequestUri?.IsAbsoluteUri != true)
			return base.SendAsync(request, cancellationToken);

		var ub = new UriBuilder(request.RequestUri);
		var path = ub.Path;

		if (Routing.IsExemptFromFaceScope(path))
			return base.SendAsync(request, cancellationToken);

		if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(path, "/api", StringComparison.OrdinalIgnoreCase))
		{
			ub.Path = _prefix + path;
			request.RequestUri = ub.Uri;
			return base.SendAsync(request, cancellationToken);
		}

		if (path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(path, "/hubs", StringComparison.OrdinalIgnoreCase))
		{
			ub.Path = _prefix + path;
			request.RequestUri = ub.Uri;
			return base.SendAsync(request, cancellationToken);
		}

		return base.SendAsync(request, cancellationToken);
	}
}
