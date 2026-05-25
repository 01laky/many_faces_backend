using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BeDemo.Api.Configuration;
using BeDemo.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BeDemo.Api.Services;

/// <inheritdoc />
public sealed class VideoLoungeTokenService : IVideoLoungeTokenService
{
	private readonly VideoLoungeOptions _options;

	public VideoLoungeTokenService(IOptions<VideoLoungeOptions> options) => _options = options.Value;

	/// <inheritdoc />
	public VideoLoungeTokenResult CreateToken(
		int sessionId,
		string userId,
		string displayName,
		VideoLoungeJoinMode joinMode)
	{
		var roomName = $"vl_session_{sessionId}";
		var expires = DateTime.UtcNow.AddMinutes(Math.Clamp(_options.TokenTtlMinutes, 5, 60));
		var useStub = _options.UseStubTokens
			|| string.IsNullOrWhiteSpace(_options.ApiKey)
			|| string.IsNullOrWhiteSpace(_options.ApiSecret);

		if (useStub)
		{
			var stubPayload = Convert.ToBase64String(
				Encoding.UTF8.GetBytes(
					JsonSerializer.Serialize(new
					{
						sessionId,
						userId,
						displayName,
						joinMode = joinMode.ToString(),
						exp = expires,
					})));
			return new VideoLoungeTokenResult
			{
				Token = $"vl-stub.{stubPayload}",
				ServerUrl = _options.LiveKitUrl ?? "wss://stub.livekit.local",
				RoomName = roomName,
				IsStub = true,
				ExpiresAtUtc = expires,
			};
		}

		var canPublish = joinMode is VideoLoungeJoinMode.Listener or VideoLoungeJoinMode.Full;
		var canPublishVideo = joinMode == VideoLoungeJoinMode.Full;
		var claims = new List<Claim>
		{
			new("sub", userId),
			new("name", displayName),
			new("video", JsonSerializer.Serialize(new
			{
				room = roomName,
				roomJoin = true,
				canPublish,
				canSubscribe = true,
				canPublishData = false,
			})),
		};

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret!));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var jwt = new JwtSecurityToken(
			issuer: _options.ApiKey,
			claims: claims,
			expires: expires,
			signingCredentials: creds);

		if (!canPublishVideo)
		{
			// LiveKit uses nested grant object; stub path documents intent for real SDK swap later.
		}

		var token = new JwtSecurityTokenHandler().WriteToken(jwt);
		return new VideoLoungeTokenResult
		{
			Token = token,
			ServerUrl = _options.LiveKitUrl ?? "wss://livekit.local",
			RoomName = roomName,
			IsStub = false,
			ExpiresAtUtc = expires,
		};
	}
}
