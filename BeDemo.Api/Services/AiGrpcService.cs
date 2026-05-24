/*
 * AiGrpcService.cs - gRPC client for Python AI service (many_faces_ai)
 *
 * BSH3-G2: TLS + optional service token via GrpcWorkerChannelFactory (parity with Search/Push/Mail).
 * Recreates the channel on connection failures (e.g. after AI container restart).
 */

using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using BeDemo.Api.Configuration;
using BeDemo.Api.Utils;
using Grpc.Core;
using Grpc.Net.Client;
using BeDemo.Api.Models;
using Health;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// Singleton gRPC client that calls the Python AI service RPCs on <see cref="HealthService.HealthServiceClient"/>.
/// </summary>
public class AiGrpcService : IAiGrpcService, IAiModelStatusClient, IDisposable
{
    private readonly AiServiceOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiGrpcService> _logger;
    private readonly object _channelLock = new();
    private readonly List<X509Certificate2> _tlsCertificatesToDispose = [];
    private readonly TimeSpan _deadline = TimeSpan.FromSeconds(300);
    private GrpcChannel? _channel;
    private string? _resolvedAddress;

    public AiGrpcService(
        IOptions<AiServiceOptions> options,
        IConfiguration configuration,
        ILogger<AiGrpcService> logger)
    {
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    private string ResolvedAddress =>
        _resolvedAddress ??= _options.ResolveGrpcAddress(_configuration);

    private GrpcChannel GetOrCreateChannel()
    {
        lock (_channelLock)
        {
            if (_channel != null)
                return _channel;

            var address = ResolvedAddress;
            _logger.LogInformation("Creating gRPC channel to AI service at {Address}", address);
            var tlsSettings = GrpcWorkerChannelFactory.FromAi(_options, address);
            var revocation = _configuration.GetValue("HardenedSecurity:CertificateRevocationMode", "NoCheck") switch
            {
                "Online" => X509RevocationMode.Online,
                "Offline" => X509RevocationMode.Offline,
                _ => X509RevocationMode.NoCheck,
            };
            _channel = GrpcWorkerChannelFactory.CreateChannel(tlsSettings, _tlsCertificatesToDispose, revocation);
            return _channel;
        }
    }

    private CallOptions CreateCallOptions(CancellationToken cancellationToken, TimeSpan? deadline = null)
    {
        var effectiveDeadline = deadline ?? _deadline;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveDeadline);

        var headers = new Metadata();
        if (!string.IsNullOrWhiteSpace(_options.WorkerAuthToken))
            headers.Add(AiServiceOptions.WorkerAuthMetadataKey, _options.WorkerAuthToken.Trim());

        return new CallOptions(headers, DateTime.UtcNow.Add(effectiveDeadline), cts.Token);
    }

    private void InvalidateChannel()
    {
        lock (_channelLock)
        {
            if (_channel != null)
            {
                try { _channel.Dispose(); } catch { /* ignore */ }
                _channel = null;
                _logger.LogInformation("gRPC channel invalidated, will recreate on next request");
            }
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(
        string prompt,
        int maxNewTokens = 50,
        string? statsContextJson = null,
        string? responseLocale = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return string.Empty;

        var request = new GenerateRequest
        {
            Prompt = prompt,
            MaxNewTokens = maxNewTokens <= 0 ? 50 : maxNewTokens,
        };
        if (!string.IsNullOrWhiteSpace(statsContextJson))
            request.StatsContextJson = statsContextJson.Trim();
        if (!string.IsNullOrWhiteSpace(responseLocale))
            request.ResponseLocale = responseLocale.Trim();

        var callOptions = CreateCallOptions(cancellationToken);

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var channel = GetOrCreateChannel();
                var client = new HealthService.HealthServiceClient(channel);
                _logger.LogInformation("Sending Generate request (attempt {Attempt}), prompt length={Length}", attempt, prompt.Length);
                var response = await client.GenerateAsync(request, callOptions);
                _logger.LogInformation("Received Generate response, text length={Length}", response.Text?.Length ?? 0);

                if (!string.IsNullOrEmpty(response.Error))
                {
                    _logger.LogWarning("AI Generate returned error: {Error}", response.Error);
                    return response.Error;
                }
                return response.Text ?? string.Empty;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Unimplemented)
            {
                _logger.LogWarning(ex, "gRPC Unavailable (attempt {Attempt}), invalidating channel", attempt);
                InvalidateChannel();
                if (attempt == 2)
                {
                    _logger.LogError("gRPC call failed after retry: {Detail}", ex.Status.Detail);
                    return $"Error: AI service unavailable ({ex.StatusCode})";
                }
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC call failed: Status={Status}", ex.StatusCode);
                return $"Error: AI service unavailable ({ex.StatusCode})";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error (attempt {Attempt}), invalidating channel", attempt);
                InvalidateChannel();
                if (attempt == 2)
                    return "Error: AI service unavailable (connection failed)";
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AI Generate timed out after {Seconds}s", _deadline.TotalSeconds);
                return "Error: AI service timed out";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI Generate failed");
                return $"Error: {ex.Message}";
            }
        }
        return "Error: AI service unavailable";
    }

    /// <inheritdoc />
    public async Task<string> OperatorStatsChatAsync(
        string userMessage,
        string historyText,
        bool fetchLivePublicSnapshot,
        string publicStatsAbsoluteUrl,
        int maxNewTokens = 150,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(publicStatsAbsoluteUrl)
            && !OutboundUrlAllowlist.TryValidatePublicHttpsUrl(publicStatsAbsoluteUrl, out var rejection))
        {
            _logger.LogWarning(
                "OperatorStatsChat rejected outbound URL reason={Reason} urlLen={UrlLen}",
                rejection,
                publicStatsAbsoluteUrl.Length);
            return "Error: outbound stats URL rejected";
        }

        var grpcRequest = new OperatorStatsChatRequest
        {
            UserMessage = userMessage ?? string.Empty,
            HistoryText = historyText ?? string.Empty,
            FetchLivePublicSnapshot = fetchLivePublicSnapshot,
            PublicStatsAbsoluteUrl = publicStatsAbsoluteUrl ?? string.Empty,
            MaxNewTokens = maxNewTokens <= 0 ? 150 : maxNewTokens,
        };

        var callOptions = CreateCallOptions(cancellationToken);

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var channel = GetOrCreateChannel();
                var client = new HealthService.HealthServiceClient(channel);
                _logger.LogInformation(
                    "Sending OperatorStatsChat (attempt {Attempt}), live={Live}, urlLen={UrlLen}",
                    attempt,
                    fetchLivePublicSnapshot,
                    grpcRequest.PublicStatsAbsoluteUrl.Length);
                var response = await client.OperatorStatsChatAsync(grpcRequest, callOptions);
                if (!string.IsNullOrEmpty(response.Error))
                {
                    _logger.LogWarning("AI OperatorStatsChat returned error: {Error}", response.Error);
                    return response.Error;
                }

                return response.Text ?? string.Empty;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Unimplemented)
            {
                _logger.LogWarning(ex, "OperatorStatsChat gRPC unavailable (attempt {Attempt})", attempt);
                InvalidateChannel();
                if (attempt == 2)
                    return $"Error: AI service unavailable ({ex.StatusCode})";
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "OperatorStatsChat gRPC failed: Status={Status}", ex.StatusCode);
                return $"Error: AI service unavailable ({ex.StatusCode})";
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AI OperatorStatsChat timed out after {Seconds}s", _deadline.TotalSeconds);
                return "Error: AI service timed out";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI OperatorStatsChat failed");
                return $"Error: {ex.Message}";
            }
        }

        return "Error: AI service unavailable";
    }

    /// <inheritdoc />
    public async Task<AiModelStatus> GetModelStatusAsync(CancellationToken cancellationToken = default)
    {
        var callOptions = CreateCallOptions(cancellationToken, TimeSpan.FromSeconds(15));

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var channel = GetOrCreateChannel();
                var client = new HealthService.HealthServiceClient(channel);
                var response = await client.HealthCheckAsync(new HealthCheckRequest(), callOptions);
                return ParseModelStatus(response.Message);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Unimplemented)
            {
                _logger.LogWarning(ex, "HealthCheck gRPC unavailable (attempt {Attempt})", attempt);
                InvalidateChannel();
                if (attempt == 2)
                    return new AiModelStatus(false, false, true, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HealthCheck failed");
                return new AiModelStatus(false, false, true, null);
            }
        }

        return new AiModelStatus(false, false, true, null);
    }

    /// <inheritdoc />
    public async Task<AiHostProfileFetchResult> GetHostProfileAsync(CancellationToken cancellationToken = default)
    {
        var callOptions = CreateCallOptions(cancellationToken, TimeSpan.FromSeconds(15));

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var channel = GetOrCreateChannel();
                var client = new HealthService.HealthServiceClient(channel);
                var response = await client.GetHostProfileAsync(new HostProfileRequest(), callOptions);
                if (!string.IsNullOrWhiteSpace(response.Error))
                {
                    _logger.LogWarning("GetHostProfile returned error: {Error}", response.Error);
                    return new AiHostProfileFetchResult(null, response.Error);
                }

                return new AiHostProfileFetchResult(response.JsonBody, null);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Unimplemented)
            {
                _logger.LogWarning(ex, "GetHostProfile gRPC unavailable/unimplemented (attempt {Attempt})", attempt);
                InvalidateChannel();
                if (attempt == 2)
                    return new AiHostProfileFetchResult(null, $"AI service unavailable ({ex.StatusCode})");
            }
            catch (RpcException ex)
            {
                _logger.LogWarning(ex, "GetHostProfile gRPC failed: Status={Status}", ex.StatusCode);
                return new AiHostProfileFetchResult(null, $"AI service unavailable ({ex.StatusCode})");
            }
            catch (OperationCanceledException)
            {
                return new AiHostProfileFetchResult(null, "AI service timed out");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetHostProfile failed");
                return new AiHostProfileFetchResult(null, ex.Message);
            }
        }

        return new AiHostProfileFetchResult(null, "AI service unavailable");
    }

    /// <inheritdoc />
    public async Task<AiContentReviewResult> ReviewContentAsync(
        AiContentReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var grpcRequest = new ContentReviewRequest
        {
            ContentType = request.ContentType.ToString(),
            ContentId = request.ContentId,
            ModerationVersion = request.ModerationVersion,
            FaceId = request.FaceId,
            Title = request.Title,
            Body = request.Body,
            MediaUrl = request.MediaUrl ?? string.Empty,
            CreatorId = request.CreatorId,
        };
        var callOptions = CreateCallOptions(cancellationToken);

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var channel = GetOrCreateChannel();
                var client = new HealthService.HealthServiceClient(channel);
                _logger.LogInformation(
                    "Sending ReviewContent request (attempt {Attempt}) for {ContentType}:{ContentId} v{ModerationVersion}",
                    attempt,
                    request.ContentType,
                    request.ContentId,
                    request.ModerationVersion);
                var response = await client.ReviewContentAsync(grpcRequest, callOptions);
                if (!string.IsNullOrWhiteSpace(response.Error))
                {
                    _logger.LogWarning("AI ReviewContent returned error: {Error}", response.Error);
                    return new AiContentReviewResult(null, response.Error);
                }

                var recommendation = new AiReviewRecommendation(
                    ParseDecision(response.Decision),
                    response.Confidence,
                    ParseRiskLevel(response.RiskLevel),
                    response.Flags.ToArray(),
                    response.Reason,
                    response.UserMessage,
                    response.ModelVersion,
                    response.TraceId);
                return new AiContentReviewResult(recommendation, null);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Unimplemented)
            {
                _logger.LogWarning(ex, "ReviewContent gRPC unavailable/unimplemented (attempt {Attempt})", attempt);
                InvalidateChannel();
                if (attempt == 2)
                    return new AiContentReviewResult(null, $"AI service unavailable ({ex.StatusCode})");
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "ReviewContent gRPC failed: Status={Status}", ex.StatusCode);
                return new AiContentReviewResult(null, $"AI service unavailable ({ex.StatusCode})");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AI ReviewContent timed out after {Seconds}s", _deadline.TotalSeconds);
                return new AiContentReviewResult(null, "AI service timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI ReviewContent failed");
                return new AiContentReviewResult(null, ex.Message);
            }
        }

        return new AiContentReviewResult(null, "AI service unavailable");
    }

    private static AiReviewDecision ParseDecision(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "approve" => AiReviewDecision.Approve,
            "reject" => AiReviewDecision.Reject,
            "needs_human_review" => AiReviewDecision.NeedsHumanReview,
            "needshumanreview" => AiReviewDecision.NeedsHumanReview,
            _ => (AiReviewDecision)(-1),
        };

    private static AiReviewRiskLevel ParseRiskLevel(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "low" => AiReviewRiskLevel.Low,
            "medium" => AiReviewRiskLevel.Medium,
            "high" => AiReviewRiskLevel.High,
            _ => AiReviewRiskLevel.Unknown,
        };

    private static AiModelStatus ParseModelStatus(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new AiModelStatus(false, true, false, null);

        var trimmed = message.Trim();
        if (!trimmed.StartsWith('{'))
            return new AiModelStatus(false, true, false, null);

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            var ready = root.TryGetProperty("ready", out var r) && r.GetBoolean();
            var loading = root.TryGetProperty("loading", out var l) && l.GetBoolean();
            var unavailable = root.TryGetProperty("unavailable", out var u) && u.GetBoolean();
            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(err.GetString()))
                unavailable = true;
            string? modelName = null;
            if (root.TryGetProperty("modelName", out var m) && m.ValueKind == JsonValueKind.String)
                modelName = m.GetString();
            return new AiModelStatus(ready, loading, unavailable, modelName);
        }
        catch (JsonException)
        {
            return new AiModelStatus(false, true, false, null);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_channelLock)
        {
            _channel?.Dispose();
            _channel = null;
        }

        foreach (var cert in _tlsCertificatesToDispose)
        {
            cert.Dispose();
        }

        _tlsCertificatesToDispose.Clear();
    }
}
