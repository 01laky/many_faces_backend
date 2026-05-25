using BeDemo.Api.Dev;
using Xunit;

namespace BeDemo.Api.Tests.Dev;

public class DevLanCorsOriginBuilderTests
{
	[Fact]
	public void Build_ReturnsEmpty_WhenHostMissingOrInvalid()
	{
		Assert.Empty(DevLanCorsOriginBuilder.Build(null));
		Assert.Empty(DevLanCorsOriginBuilder.Build(""));
		Assert.Empty(DevLanCorsOriginBuilder.Build("not-an-ip"));
	}

	[Fact]
	public void Build_IncludesPortalAndAdminProxyOrigins_ForIpv4()
	{
		var origins = DevLanCorsOriginBuilder.Build("172.20.10.14");
		Assert.Contains("http://172.20.10.14:9080", origins);
		Assert.Contains("http://172.20.10.14:8090", origins);
		Assert.Contains("https://172.20.10.14:9081", origins);
		Assert.Contains("https://172.20.10.14:8091", origins);
		Assert.Contains("https://172.20.10.14:8001", origins);
	}
}
