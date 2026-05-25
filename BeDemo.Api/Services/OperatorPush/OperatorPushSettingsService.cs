using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.Admin;
using BeDemo.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorPush;

/// <summary>
/// L1 cache for operator push settings. DB row overrides env/bootstrap on read; insert-on-first-read seeds from <see cref="PushOptions"/>.
/// </summary>
public sealed class OperatorPushSettingsService : IOperatorPushSettingsProvider
{
	private const string MemoryCacheKey = "OperatorPush:SystemSettings";

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IMemoryCache _memoryCache;
	private readonly PushOptions _pushOptions;
	private readonly PushFirebaseBootstrapOptions _firebaseBootstrap;
	private readonly IOperatorPushSecretProtector _secretProtector;

	public OperatorPushSettingsService(
		IServiceScopeFactory scopeFactory,
		IMemoryCache memoryCache,
		IOptions<PushOptions> pushOptions,
		IOptions<PushFirebaseBootstrapOptions> firebaseBootstrap,
		IOperatorPushSecretProtector secretProtector)
	{
		_scopeFactory = scopeFactory;
		_memoryCache = memoryCache;
		_pushOptions = pushOptions.Value;
		_firebaseBootstrap = firebaseBootstrap.Value;
		_secretProtector = secretProtector;
	}

	/// <inheritdoc />
	public async Task<OperatorPushSettingsValues> GetAsync(CancellationToken cancellationToken = default)
	{
		if (_memoryCache.TryGetValue(MemoryCacheKey, out OperatorPushSettingsValues? cached) && cached != null)
			return cached;

		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var row = await db.OperatorPushSystemSettings
			.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);

		if (row == null)
		{
			row = CreateBootstrapRow();
			db.OperatorPushSystemSettings.Add(row);
			await db.SaveChangesAsync(cancellationToken);
		}

		var values = ToValues(row);
		Cache(values);
		return values;
	}

	/// <inheritdoc />
	public async Task<OperatorPushSettingsValues> SetAsync(
		OperatorPushSettingsValues values,
		CancellationToken cancellationToken = default)
	{
		using var scope = _scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var row = await db.OperatorPushSystemSettings.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);
		if (row == null)
		{
			row = CreateBootstrapRow();
			db.OperatorPushSystemSettings.Add(row);
		}

		ApplyValues(row, values);
		await db.SaveChangesAsync(cancellationToken);

		Cache(values);
		return values;
	}

	/// <inheritdoc />
	public AdminPushSettingsDto ToDto(OperatorPushSettingsValues values, PushOptions envOptions) => new()
	{
		Enabled = values.Enabled,
		WorkerGrpcUrl = values.WorkerGrpcUrl,
		HasWorkerAuthToken = values.HasWorkerAuthToken,
		Firebase = new AdminPushFirebaseSettingsDto
		{
			ProjectId = values.FirebaseProjectId,
			HasCredentials = values.HasFirebaseCredentials,
		},
		Defaults = new AdminPushDefaultsSettingsDto
		{
			TitleLocKey = values.DefaultTitleLocKey,
			BodyLocKey = values.DefaultBodyLocKey,
			AndroidChannelId = values.DefaultAndroidChannelId,
		},
		GrpcDeadlineSeconds = values.GrpcDeadlineSeconds,
		Transport = new AdminPushTransportSettingsDto
		{
			TlsConfiguredViaEnv = !string.IsNullOrWhiteSpace(envOptions.WorkerTlsServerCaPath),
			MtlsConfiguredViaEnv = !string.IsNullOrWhiteSpace(envOptions.WorkerTlsClientCertPath),
		},
		EffectiveStatus = values.EffectiveStatus,
		UpdatedAtUtc = values.UpdatedAtUtc,
		UpdatedByUserId = values.UpdatedByUserId,
	};

	/// <inheritdoc />
	public void InvalidateCache() => _memoryCache.Remove(MemoryCacheKey);

	private OperatorPushSystemSettings CreateBootstrapRow()
	{
		var row = new OperatorPushSystemSettings
		{
			Id = 1,
			Enabled = _pushOptions.Enabled,
			WorkerGrpcUrl = _pushOptions.WorkerGrpcUrl,
			DefaultTitleLocKey = _pushOptions.DefaultTitleLocKey,
			DefaultBodyLocKey = _pushOptions.DefaultBodyLocKey,
			DefaultAndroidChannelId = _pushOptions.DefaultAndroidChannelId,
			GrpcDeadlineSeconds = _pushOptions.GrpcDeadlineSeconds,
			UpdatedAtUtc = DateTime.UtcNow,
		};

		if (!string.IsNullOrWhiteSpace(_pushOptions.WorkerAuthToken))
			row.WorkerAuthTokenCiphertext = _secretProtector.Protect(_pushOptions.WorkerAuthToken.Trim());

		TryBootstrapFirebaseJson(row);
		return row;
	}

	private void TryBootstrapFirebaseJson(OperatorPushSystemSettings row)
	{
		var path = _firebaseBootstrap.ServiceAccountPath;
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			return;

		try
		{
			var json = File.ReadAllText(path);
			if (!FirebaseServiceAccountValidator.TryValidate(json, out var projectId, out _))
				return;

			row.FirebaseProjectId = projectId;
			row.FirebaseServiceAccountJsonCiphertext = _secretProtector.Protect(json);
		}
		catch
		{
			// Bootstrap is best-effort only.
		}
	}

	private OperatorPushSettingsValues ToValues(OperatorPushSystemSettings row) => new(
		row.Enabled,
		row.WorkerGrpcUrl,
		UnprotectOrNull(row.WorkerAuthTokenCiphertext),
		row.FirebaseProjectId,
		UnprotectOrNull(row.FirebaseServiceAccountJsonCiphertext),
		row.DefaultTitleLocKey,
		row.DefaultBodyLocKey,
		row.DefaultAndroidChannelId,
		row.GrpcDeadlineSeconds,
		row.UpdatedAtUtc,
		row.UpdatedByUserId);

	private void ApplyValues(OperatorPushSystemSettings row, OperatorPushSettingsValues values)
	{
		row.Enabled = values.Enabled;
		row.WorkerGrpcUrl = values.WorkerGrpcUrl;
		row.WorkerAuthTokenCiphertext = string.IsNullOrWhiteSpace(values.WorkerAuthTokenPlaintext)
			? null
			: _secretProtector.Protect(values.WorkerAuthTokenPlaintext.Trim());
		row.FirebaseProjectId = values.FirebaseProjectId;
		row.FirebaseServiceAccountJsonCiphertext = string.IsNullOrWhiteSpace(values.FirebaseServiceAccountJsonPlaintext)
			? null
			: _secretProtector.Protect(values.FirebaseServiceAccountJsonPlaintext);
		row.DefaultTitleLocKey = values.DefaultTitleLocKey;
		row.DefaultBodyLocKey = values.DefaultBodyLocKey;
		row.DefaultAndroidChannelId = values.DefaultAndroidChannelId;
		row.GrpcDeadlineSeconds = values.GrpcDeadlineSeconds;
		row.UpdatedAtUtc = values.UpdatedAtUtc;
		row.UpdatedByUserId = values.UpdatedByUserId;
	}

	private string? UnprotectOrNull(string? ciphertext)
	{
		if (string.IsNullOrWhiteSpace(ciphertext))
			return null;

		try
		{
			return _secretProtector.Unprotect(ciphertext);
		}
		catch
		{
			return null;
		}
	}

	private void Cache(OperatorPushSettingsValues values)
	{
		_memoryCache.Set(
			MemoryCacheKey,
			values,
			new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
			});
	}
}
