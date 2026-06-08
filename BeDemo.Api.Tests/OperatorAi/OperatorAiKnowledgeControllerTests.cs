using System.Security.Claims;
using BeDemo.Api.Controllers;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using ManyFaces.Search.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// Edge cases for the operator-AI knowledge admin endpoints (§7.2/§8.1/§17.9). Both are SUPER_ADMIN-only
/// (`CanManageAllFaces`); reindex coalesces to 409 (RT-17), surfaces 503 on worker/embed failure, and is gated by
/// the global AI switch (RT-13); status returns 503 when the worker is unreachable so the panel can show degraded.
/// </summary>
public sealed class OperatorAiKnowledgeControllerTests
{
	private static OperatorAiKnowledgeController Build(
		Mock<IAccessEvaluator> access,
		Mock<IOperatorAiKnowledgeIndexer> indexer,
		Mock<IOperatorAiKnowledgeStatusCache> status,
		Mock<IOperatorAiSystemSettingsProvider> settings)
	{
		var controller = new OperatorAiKnowledgeController(
			access.Object, indexer.Object, status.Object, settings.Object,
			NullLogger<OperatorAiKnowledgeController>.Instance)
		{
			ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
		};
		return controller;
	}

	private static (Mock<IAccessEvaluator>, Mock<IOperatorAiKnowledgeIndexer>, Mock<IOperatorAiKnowledgeStatusCache>, Mock<IOperatorAiSystemSettingsProvider>) Mocks(
		bool isOperator = true, bool aiEnabled = true)
	{
		var access = new Mock<IAccessEvaluator>();
		access.Setup(a => a.CanManageAllFaces(It.IsAny<ClaimsPrincipal>())).Returns(isOperator);
		var settings = new Mock<IOperatorAiSystemSettingsProvider>();
		settings.Setup(s => s.IsAiEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(aiEnabled);
		return (access, new Mock<IOperatorAiKnowledgeIndexer>(), new Mock<IOperatorAiKnowledgeStatusCache>(), settings);
	}

	private static OperatorAiKnowledgeReindexResult Result(int indexed = 0, int failed = 0, string? model = null, bool skipped = false, bool coalesced = false, string? error = null)
		=> new(indexed, failed, model, skipped, coalesced, error);

	[Fact]
	public async Task Reindex_denies_non_operator()
	{
		var (access, indexer, status, settings) = Mocks(isOperator: false);
		var result = await Build(access, indexer, status, settings).Reindex(default);
		result.Should().BeOfType<ForbidResult>();
		indexer.Verify(i => i.RebuildAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task Reindex_conflicts_when_ai_disabled()
	{
		var (access, indexer, status, settings) = Mocks(aiEnabled: false);
		var result = await Build(access, indexer, status, settings).Reindex(default);
		result.Should().BeOfType<ConflictObjectResult>();
		indexer.Verify(i => i.RebuildAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task Reindex_returns_409_when_coalesced()
	{
		var (access, indexer, status, settings) = Mocks();
		indexer.Setup(i => i.RebuildAsync(true, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result(coalesced: true, error: "reindex_already_running"));

		var result = await Build(access, indexer, status, settings).Reindex(default);
		result.Should().BeOfType<ConflictObjectResult>();
	}

	[Fact]
	public async Task Reindex_returns_503_on_worker_failure()
	{
		var (access, indexer, status, settings) = Mocks();
		indexer.Setup(i => i.RebuildAsync(true, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result(error: "embed_unavailable"));

		var result = await Build(access, indexer, status, settings).Reindex(default);
		result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
	}

	[Fact]
	public async Task Reindex_returns_ok_on_success()
	{
		var (access, indexer, status, settings) = Mocks();
		indexer.Setup(i => i.RebuildAsync(true, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result(indexed: 61, model: "nomic-embed-text"));

		var result = await Build(access, indexer, status, settings).Reindex(default);
		result.Should().BeOfType<OkObjectResult>();
	}

	[Fact]
	public async Task Reindex_returns_ok_on_idempotent_skip()
	{
		var (access, indexer, status, settings) = Mocks();
		indexer.Setup(i => i.RebuildAsync(true, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result(skipped: true, model: "nomic-embed-text"));

		var result = await Build(access, indexer, status, settings).Reindex(default);
		result.Should().BeOfType<OkObjectResult>("an idempotent skip is a success, not an error");
	}

	[Fact]
	public async Task Status_denies_non_operator()
	{
		var (access, indexer, status, settings) = Mocks(isOperator: false);
		var result = await Build(access, indexer, status, settings).Status(default);
		result.Should().BeOfType<ForbidResult>();
	}

	[Fact]
	public async Task Status_returns_503_when_worker_unavailable()
	{
		var (access, indexer, status, settings) = Mocks();
		status.Setup(s => s.GetStatusAsync(true, It.IsAny<CancellationToken>())).ReturnsAsync((KnowledgeIndexStatusResponse?)null);

		var result = await Build(access, indexer, status, settings).Status(default);
		result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
	}

	[Fact]
	public async Task Status_returns_ok_with_payload()
	{
		var (access, indexer, status, settings) = Mocks();
		status.Setup(s => s.GetStatusAsync(true, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new KnowledgeIndexStatusResponse { Ready = true, DocCount = 61, ExpectedDocCount = 61, EmbedModelVersion = "nomic-embed-text", VectorDim = 768 });

		var result = await Build(access, indexer, status, settings).Status(default);
		result.Should().BeOfType<OkObjectResult>();
	}
}
