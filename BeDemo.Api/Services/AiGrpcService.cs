/*
 * AiGrpcService.cs - gRPC client for Python AI service (many_faces_ai)
 *
 * Calls the Generate RPC on the AI service. Address is read from configuration
 * (AiService:GrpcAddress or AI_SERVICE_GRPC_ADDRESS), e.g. http://ai-demo-dev:50051.
 * Recreates the channel on connection failures (e.g. after AI container restart).
 */

using System.Text.Json;
using Grpc.Core;
using Grpc.Net.Client;
using BeDemo.Api.Models;
using Health;

namespace BeDemo.Api.Services;

/// <summary>
/// Singleton gRPC client that calls the Python AI service Generate RPC.
/// Recreates the channel when connection fails (e.g. AI container restarted).
/// </summary>
public class AiGrpcService : IAiGrpcService, IDisposable
{
    private readonly string _grpcAddress;
    private readonly ILogger<AiGrpcService> _logger;
    private readonly object _channelLock = new();
    private GrpcChannel? _channel;
    private readonly TimeSpan _deadline = TimeSpan.FromSeconds(300);

    public AiGrpcService(IConfiguration configuration, ILogger<AiGrpcService> logger)
    {
        _grpcAddress = configuration["AiService:GrpcAddress"]
            ?? Environment.GetEnvironmentVariable("AI_SERVICE_GRPC_ADDRESS")
            ?? "http://ai-demo-dev:50051";
        _logger = logger;
    }

    private GrpcChannel GetOrCreateChannel()
    {
        lock (_channelLock)
        {
            if (_channel != null)
                return _channel;
            _logger.LogInformation("Creating gRPC channel to AI service at {Address}", _grpcAddress);
            var handler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            };
            _channel = GrpcChannel.ForAddress(_grpcAddress, new GrpcChannelOptions { HttpHandler = handler });
            return _channel;
        }
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
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_deadline);
        var callOptions = new CallOptions(deadline: DateTime.UtcNow.Add(_deadline), cancellationToken: cts.Token);

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
        var grpcRequest = new OperatorStatsChatRequest
        {
            UserMessage = userMessage ?? string.Empty,
            HistoryText = historyText ?? string.Empty,
            FetchLivePublicSnapshot = fetchLivePublicSnapshot,
            PublicStatsAbsoluteUrl = publicStatsAbsoluteUrl ?? string.Empty,
            MaxNewTokens = maxNewTokens <= 0 ? 150 : maxNewTokens,
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_deadline);
        var callOptions = new CallOptions(deadline: DateTime.UtcNow.Add(_deadline), cancellationToken: cts.Token);

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
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        var callOptions = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(15), cancellationToken: cts.Token);

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
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_deadline);
        var callOptions = new CallOptions(deadline: DateTime.UtcNow.Add(_deadline), cancellationToken: cts.Token);

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

    public void Dispose()
    {
        lock (_channelLock)
        {
            _channel?.Dispose();
            _channel = null;
        }
    }
}
