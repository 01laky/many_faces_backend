using BeDemo.Api.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Utils;

/// <summary>BE-RP29 — optional EF <c>TagWith</c> for dev profiling.</summary>
public static class EfQueryTags
{
	public const string FacesConfig = "BE-RP:faces-config:GetFacesConfig";
	public const string Conversations = "BE-RP:messenger:GetConversations";
	public const string Capabilities = "BE-RP:me:GetCapabilities";
	public const string PlatformStatsDashboard = "BE-RP:stats:GetOperatorDashboardSummary";
	public const string PlatformStatsPublic = "BE-RP:stats:GetPublicSnapshot";
	public const string GridSnapshot = "BE-RP:grid:FaceGridSnapshot";

	public static IQueryable<T> TagIfEnabled<T>(
		this IQueryable<T> query,
		IOptions<PerformanceOptions> options,
		string tag) =>
		options.Value.EfQueryTagsEnabled ? query.TagWith(tag) : query;
}
