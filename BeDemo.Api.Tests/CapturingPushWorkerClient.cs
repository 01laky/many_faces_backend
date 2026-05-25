using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorPush;
using ManyFaces.Push.V1;

namespace BeDemo.Api.Tests;

public sealed class CapturingPushWorkerClient : IPushWorkerClient
{
	private readonly IOperatorPushSettingsProvider _settings;

	public CapturingPushWorkerClient(IOperatorPushSettingsProvider settings)
	{
		_settings = settings;
	}

	public SendPushRequest? LastRequest { get; private set; }

	public TestFcmCredentialsResponse? LastTestResponse { get; private set; }

	public void Reset()
	{
		LastRequest = null;
		LastTestResponse = null;
	}

	public async Task<SendPushResponse?> SendPushAsync(
		SendPushRequest request,
		CancellationToken cancellationToken = default)
	{
		var runtime = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
		if (!runtime.IsSendAllowed)
			return null;

		request = OperatorPushProtoMapper.EnrichRequest(request, runtime);
		LastRequest = request;
		return new SendPushResponse { Sent = request.RegistrationTokens.Count };
	}

	public async Task<TestFcmCredentialsResponse?> TestFcmCredentialsAsync(
		OperatorPushSettingsValues settings,
		CancellationToken cancellationToken = default)
	{
		var runtime = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
		if (!runtime.HasFirebaseCredentials && !settings.HasFirebaseCredentials)
			return new TestFcmCredentialsResponse { Valid = false, Detail = "incomplete" };

		LastTestResponse = new TestFcmCredentialsResponse
		{
			Valid = true,
			ProjectId = settings.FirebaseProjectId ?? runtime.FirebaseProjectId ?? "demo-project",
			Detail = "ok",
		};
		return LastTestResponse;
	}
}

/// <summary>Simulates push worker disabled — <see cref="SendPushAsync"/> returns null.</summary>
public sealed class DisabledPushWorkerClient : IPushWorkerClient
{
	public Task<SendPushResponse?> SendPushAsync(
		SendPushRequest request,
		CancellationToken cancellationToken = default) =>
		Task.FromResult<SendPushResponse?>(null);

	public Task<TestFcmCredentialsResponse?> TestFcmCredentialsAsync(
		OperatorPushSettingsValues settings,
		CancellationToken cancellationToken = default) =>
		Task.FromResult<TestFcmCredentialsResponse?>(null);
}
