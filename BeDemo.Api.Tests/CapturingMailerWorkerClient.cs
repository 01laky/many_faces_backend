using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorMail;
using ManyFaces.Mailer.V1;

namespace BeDemo.Api.Tests;

public sealed class CapturingMailerWorkerClient : IMailerWorkerClient
{
	private readonly IOperatorMailSettingsProvider _settings;

	public CapturingMailerWorkerClient(IOperatorMailSettingsProvider settings)
	{
		_settings = settings;
	}

	public SendTemplatedEmailRequest? LastRequest { get; private set; }

	public TestSmtpConnectionResponse? LastTestResponse { get; private set; }

	public void Reset()
	{
		LastRequest = null;
		LastTestResponse = null;
	}

	public async Task<SendTemplatedEmailResponse?> SendTemplatedEmailAsync(
		SendTemplatedEmailRequest request,
		CancellationToken cancellationToken = default)
	{
		var runtime = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
		if (!runtime.IsSendAllowed)
			return null;

		request = OperatorMailProtoMapper.EnrichRequest(request, runtime);
		LastRequest = request;
		return new SendTemplatedEmailResponse { CorrelationId = "test-corr" };
	}

	public async Task<TestSmtpConnectionResponse?> TestSmtpConnectionAsync(
		OperatorMailSettingsValues settings,
		CancellationToken cancellationToken = default)
	{
		var runtime = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
		if (!runtime.IsSmtpComplete)
			return new TestSmtpConnectionResponse { Reachable = false, Detail = "incomplete" };

		LastTestResponse = new TestSmtpConnectionResponse { Reachable = true, Detail = "ok" };
		return LastTestResponse;
	}

	public void Dispose()
	{
	}
}

/// <summary>Simulates mail worker disabled — <see cref="SendTemplatedEmailAsync"/> returns null.</summary>
public sealed class DisabledMailerWorkerClient : IMailerWorkerClient
{
	public Task<SendTemplatedEmailResponse?> SendTemplatedEmailAsync(
		SendTemplatedEmailRequest request,
		CancellationToken cancellationToken = default) =>
		Task.FromResult<SendTemplatedEmailResponse?>(null);

	public Task<TestSmtpConnectionResponse?> TestSmtpConnectionAsync(
		OperatorMailSettingsValues settings,
		CancellationToken cancellationToken = default) =>
		Task.FromResult<TestSmtpConnectionResponse?>(null);

	public void Dispose()
	{
	}
}
