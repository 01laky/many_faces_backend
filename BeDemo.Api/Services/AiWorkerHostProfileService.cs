using System.Text.Json;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.OperatorAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// Persists AI worker host profiles and exposes operator read model.
/// </summary>
public sealed class AiWorkerHostProfileService : IAiWorkerHostProfileService
{
	private const int RefreshMetaSingletonId = 1;

	private readonly ApplicationDbContext _db;
	private readonly IAiGrpcService _aiGrpc;
	private readonly IConfiguration _configuration;
	private readonly IOptions<AiServiceOptions> _options;
	private readonly ILogger<AiWorkerHostProfileService> _logger;

	public AiWorkerHostProfileService(
		ApplicationDbContext db,
		IAiGrpcService aiGrpc,
		IConfiguration configuration,
		IOptions<AiServiceOptions> options,
		ILogger<AiWorkerHostProfileService> logger)
	{
		_db = db;
		_aiGrpc = aiGrpc;
		_configuration = configuration;
		_options = options;
		_logger = logger;
	}

	public async Task RefreshFromWorkerAsync(CancellationToken cancellationToken = default)
	{
		var grpcAddress = ResolveGrpcAddress();
		var meta = await EnsureRefreshMetaAsync(cancellationToken);
		meta.LastRefreshAttemptUtc = DateTime.UtcNow;
		meta.GrpcAddressConfigured = grpcAddress;

		var fetch = await _aiGrpc.GetHostProfileAsync(cancellationToken);
		if (!string.IsNullOrWhiteSpace(fetch.Error))
		{
			meta.LastRefreshSucceeded = false;
			meta.LastRefreshError = fetch.Error;
			await _db.SaveChangesAsync(cancellationToken);
			_logger.LogWarning("AI worker host profile refresh failed: {Error}", fetch.Error);
			return;
		}

		if (string.IsNullOrWhiteSpace(fetch.JsonBody))
		{
			meta.LastRefreshSucceeded = false;
			meta.LastRefreshError = "empty profile";
			await _db.SaveChangesAsync(cancellationToken);
			_logger.LogWarning("AI worker host profile refresh returned empty JSON");
			return;
		}

		try
		{
			using var doc = JsonDocument.Parse(fetch.JsonBody);
			var root = doc.RootElement;
			var workerId = root.TryGetProperty("workerInstanceId", out var wid)
				? wid.GetString()
				: null;
			if (string.IsNullOrWhiteSpace(workerId))
			{
				meta.LastRefreshSucceeded = false;
				meta.LastRefreshError = "missing workerInstanceId";
				await _db.SaveChangesAsync(cancellationToken);
				return;
			}

			var collectedAt = ParseCollectedAtUtc(root);
			var entity = await _db.AiWorkerHostProfiles
				.FirstOrDefaultAsync(p => p.WorkerInstanceId == workerId, cancellationToken);
			if (entity == null)
			{
				entity = new AiWorkerHostProfile { WorkerInstanceId = workerId };
				_db.AiWorkerHostProfiles.Add(entity);
			}

			entity.CollectedAtUtc = collectedAt;
			entity.GrpcAddressLastSeen = grpcAddress;
			entity.ProfileJson = fetch.JsonBody;
			entity.Hostname = ReadString(root, "hostname");
			entity.OsDisplayName = ReadNestedString(root, "os", "displayName");
			entity.CpuLogicalCores = ReadNestedInt(root, "cpu", "logicalCores");
			entity.GpuPrimaryName = ReadPrimaryGpuName(root);
			entity.GpuVramBytes = ReadPrimaryGpuVram(root);
			entity.RamTotalBytes = ReadNestedLong(root, "memory", "ramTotalBytes");
			entity.UpdatedAtUtc = DateTime.UtcNow;

			meta.LastRefreshSucceeded = true;
			meta.LastRefreshError = null;
			await _db.SaveChangesAsync(cancellationToken);
			_logger.LogInformation(
				"AI worker host profile refreshed for {WorkerId} ({Hostname})",
				workerId,
				entity.Hostname);
		}
		catch (JsonException ex)
		{
			meta.LastRefreshSucceeded = false;
			meta.LastRefreshError = "invalid profile JSON";
			await _db.SaveChangesAsync(cancellationToken);
			_logger.LogWarning(ex, "AI worker host profile JSON parse failed");
		}
	}

	public async Task<OperatorAiWorkerHostDto> GetOperatorViewAsync(CancellationToken cancellationToken = default)
	{
		var meta = await _db.AiWorkerHostRefreshMetas
			.AsNoTracking()
			.FirstOrDefaultAsync(m => m.Id == RefreshMetaSingletonId, cancellationToken);

		var latest = await _db.AiWorkerHostProfiles
			.AsNoTracking()
			.OrderByDescending(p => p.UpdatedAtUtc)
			.FirstOrDefaultAsync(cancellationToken);

		JsonElement? profile = null;
		if (latest != null && !string.IsNullOrWhiteSpace(latest.ProfileJson))
		{
			try
			{
				using var doc = JsonDocument.Parse(latest.ProfileJson);
				profile = doc.RootElement.Clone();
			}
			catch (JsonException ex)
			{
				_logger.LogDebug(ex, "Stored host profile JSON could not be parsed for operator view");
			}
		}

		return new OperatorAiWorkerHostDto
		{
			Reachable = meta?.LastRefreshSucceeded == true,
			LastRefreshAttemptUtc = meta?.LastRefreshAttemptUtc,
			LastRefreshError = meta?.LastRefreshError,
			GrpcAddressConfigured = meta?.GrpcAddressConfigured ?? ResolveGrpcAddress(),
			Profile = profile,
		};
	}

	private string ResolveGrpcAddress() =>
		_configuration["AiService:GrpcAddress"]
		?? Environment.GetEnvironmentVariable("AI_SERVICE_GRPC_ADDRESS")
		?? _options.Value.GrpcAddress;

	private async Task<AiWorkerHostRefreshMeta> EnsureRefreshMetaAsync(CancellationToken cancellationToken)
	{
		var meta = await _db.AiWorkerHostRefreshMetas
			.FirstOrDefaultAsync(m => m.Id == RefreshMetaSingletonId, cancellationToken);
		if (meta != null)
			return meta;

		meta = new AiWorkerHostRefreshMeta { Id = RefreshMetaSingletonId };
		_db.AiWorkerHostRefreshMetas.Add(meta);
		return meta;
	}

	private static DateTime ParseCollectedAtUtc(JsonElement root)
	{
		if (root.TryGetProperty("collectedAtUtc", out var value)
			&& value.ValueKind == JsonValueKind.String
			&& DateTime.TryParse(value.GetString(), out var parsed))
			return parsed.ToUniversalTime();
		return DateTime.UtcNow;
	}

	private static string? ReadString(JsonElement root, string name) =>
		root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	private static string? ReadNestedString(JsonElement root, string obj, string name)
	{
		if (!root.TryGetProperty(obj, out var nested) || nested.ValueKind != JsonValueKind.Object)
			return null;
		return ReadString(nested, name);
	}

	private static int? ReadNestedInt(JsonElement root, string obj, string name)
	{
		if (!root.TryGetProperty(obj, out var nested) || nested.ValueKind != JsonValueKind.Object)
			return null;
		if (!nested.TryGetProperty(name, out var value))
			return null;
		return value.TryGetInt32(out var i) ? i : null;
	}

	private static long? ReadNestedLong(JsonElement root, string obj, string name)
	{
		if (!root.TryGetProperty(obj, out var nested) || nested.ValueKind != JsonValueKind.Object)
			return null;
		if (!nested.TryGetProperty(name, out var value))
			return null;
		if (value.TryGetInt64(out var l))
			return l;
		if (value.TryGetDouble(out var d))
			return (long)d;
		return null;
	}

	private static string? ReadPrimaryGpuName(JsonElement root)
	{
		if (!root.TryGetProperty("gpu", out var gpu) || gpu.ValueKind != JsonValueKind.Object)
			return null;
		if (!gpu.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array)
			return null;
		foreach (var device in devices.EnumerateArray())
		{
			var name = ReadString(device, "name");
			if (!string.IsNullOrWhiteSpace(name))
				return name;
		}
		return null;
	}

	private static long? ReadPrimaryGpuVram(JsonElement root)
	{
		if (!root.TryGetProperty("gpu", out var gpu) || gpu.ValueKind != JsonValueKind.Object)
			return null;
		if (!gpu.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array)
			return null;
		foreach (var device in devices.EnumerateArray())
		{
			if (device.TryGetProperty("vramBytes", out var vram))
			{
				if (vram.TryGetInt64(out var l))
					return l;
				if (vram.TryGetDouble(out var d))
					return (long)d;
			}
		}
		return null;
	}
}
