using System;
using System.Threading.Tasks;
using BeDemo.Api.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

public class OAuth2ServiceTests
{
	private readonly Mock<IECDSAKeyService> _mockKeyService;
	private readonly Mock<ILogger<OAuth2Service>> _mockLogger;
	private readonly Mock<IOAuthRefreshTokenStore> _mockRefreshStore;
	private readonly IConfiguration _configuration;
	private readonly ApplicationDbContext _db;
	private readonly IPasswordHasher<OAuthClient> _oauthClientHasher = new PasswordHasher<OAuthClient>();

	public OAuth2ServiceTests()
	{
		_mockKeyService = new Mock<IECDSAKeyService>();
		_mockLogger = new Mock<ILogger<OAuth2Service>>();
		_mockRefreshStore = new Mock<IOAuthRefreshTokenStore>();
		_mockRefreshStore
			.Setup(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		// Setup configuration
		var configBuilder = new ConfigurationBuilder();
		configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
		{
			{ "OAuth2:ClientId", "test-client" },
			{ "OAuth2:ClientSecret", "test-secret" },
			{ "Jwt:Issuer", "TestApi" },
			{ "Jwt:Audience", "TestApi" },
			{ "Jwt:ExpiresInMinutes", "60" }
		});
		_configuration = configBuilder.Build();

		var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase($"oauth2svc-tests-{Guid.NewGuid():N}")
			.Options;
		_db = new ApplicationDbContext(dbOptions);

		var oauth = new OAuthClient
		{
			ClientId = "test-client",
			IsActive = true,
			CreatedAtUtc = DateTime.UtcNow,
		};
		oauth.SecretHash = _oauthClientHasher.HashPassword(oauth, "test-secret");
		_db.OAuthClients.Add(oauth);
		_db.SaveChanges();

		// Setup mock key service
		_mockKeyService.Setup(x => x.GetKeyId()).Returns("test-key-id");
		var ecdsa = System.Security.Cryptography.ECDsa.Create();
		var securityKey = new Microsoft.IdentityModel.Tokens.ECDsaSecurityKey(ecdsa);
		_mockKeyService.Setup(x => x.GetSigningKey()).Returns(securityKey);
		_mockKeyService.Setup(x => x.GetValidationKey()).Returns(securityKey);
		_mockKeyService.Setup(x => x.GetIssuerSigningKeys()).Returns(new List<Microsoft.IdentityModel.Tokens.SecurityKey> { securityKey }.AsReadOnly());
	}

	private OAuth2Service CreateService()
	{
		var accessFactory = new OAuthAccessTokenFactory(
			_mockKeyService.Object,
			_configuration,
			Microsoft.Extensions.Options.Options.Create(new BeDemo.Api.Configuration.JwtTokenLifetimeOptions
			{
				ExpiresInMinutes = _configuration.GetValue("Jwt:ExpiresInMinutes", 60),
				ExpiresInMinutesRememberMe = _configuration.GetValue(
					"Jwt:ExpiresInMinutesRememberMe",
					BeDemo.Api.Configuration.JwtTokenLifetimeOptions.RecommendedRememberMeAccessMinutes),
			}),
			_db,
			NullLogger<OAuthAccessTokenFactory>.Instance);
		var clientValidator = new OAuthClientValidator(_db, _oauthClientHasher, NullLogger<OAuthClientValidator>.Instance);
		var signatureVerifier = new OAuthTokenRequestSignatureVerifier(
			_mockKeyService.Object,
			NullLogger<OAuthTokenRequestSignatureVerifier>.Instance,
			new SystemUtcClock());
		return new OAuth2Service(
			accessFactory,
			clientValidator,
			signatureVerifier,
			_mockRefreshStore.Object,
			_mockLogger.Object);
	}

	[Fact]
	public async Task ValidateClientAsync_ShouldReturnTrue_WhenCredentialsAreValid()
	{
		// Arrange
		var service = CreateService();

		// Act
		var result = await service.ValidateClientAsync("test-client", "test-secret");

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task ValidateClientAsync_ShouldReturnFalse_WhenClientIdIsInvalid()
	{
		// Arrange
		var service = CreateService();

		// Act
		var result = await service.ValidateClientAsync("invalid-client", "test-secret");

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task ValidateClientAsync_ShouldReturnFalse_WhenClientSecretIsInvalid()
	{
		// Arrange
		var service = CreateService();

		// Act
		var result = await service.ValidateClientAsync("test-client", "invalid-secret");

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task ValidateClientAsync_ShouldReturnFalse_WhenClientIdIsNull()
	{
		// Arrange
		var service = CreateService();

		// Act
		var result = await service.ValidateClientAsync(null, "test-secret");

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task ValidateClientAsync_ShouldReturnFalse_WhenClientSecretIsNull()
	{
		// Arrange
		var service = CreateService();

		// Act
		var result = await service.ValidateClientAsync("test-client", null);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public void ValidateRequestSignature_ShouldReturnFalse_WhenSignatureIsMissing()
	{
		// Arrange
		var service = CreateService();
		var request = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "test-client",
			ClientSecret = "test-secret"
		};

		// Act
		var result = service.ValidateRequestSignature(request);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task GenerateTokenAsync_WhenPasswordInvalid_LogsRedactedCredentialHintNotRawUsername()
	{
		const string username = "never-log-raw@example.com";
		var userStore = new Mock<IUserStore<ApplicationUser>>();
		var userManager = new Mock<UserManager<ApplicationUser>>(
			userStore.Object,
			null!,
			null!,
			null!,
			null!,
			null!,
			null!,
			null!,
			null!);

		userManager
			.Setup(m => m.FindByEmailAsync(username))
			.ReturnsAsync((ApplicationUser?)null);
		userManager
			.Setup(m => m.FindByNameAsync(username))
			.ReturnsAsync((ApplicationUser?)null);

		var service = CreateService();
		var request = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "test-client",
			ClientSecret = "test-secret",
			Username = username,
			Password = "WrongPassword1!",
		};

		_mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
		var loggedRedacted = false;
		_mockLogger
			.Setup(x => x.Log(
				It.IsAny<LogLevel>(),
				It.IsAny<EventId>(),
				It.IsAny<It.IsAnyType>(),
				It.IsAny<Exception?>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
			.Callback(new InvocationAction(invocation =>
			{
				if (invocation.Arguments[0] is not LogLevel.Warning)
					return;
				var text = invocation.Arguments[2]?.ToString() ?? string.Empty;
				if (text.Contains("credentialHintSha256Prefix=", StringComparison.Ordinal)
					&& !text.Contains(username, StringComparison.Ordinal))
					loggedRedacted = true;
			}));

		var token = await service.GenerateTokenAsync(request, userManager.Object);

		token.Should().BeNull();
		loggedRedacted.Should().BeTrue("failed password grant must log redacted credential hint only");
	}

	[Fact]
	public void ValidateRequestSignature_ShouldReturnFalse_WhenAlgorithmIsMissing()
	{
		// Arrange
		var service = CreateService();
		var request = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "test-client",
			ClientSecret = "test-secret",
			Signature = "test-signature"
		};

		// Act
		var result = service.ValidateRequestSignature(request);

		// Assert
		result.Should().BeFalse();
	}
}
