using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BeDemo.Api.Tests.TestDoubles;

internal sealed class HostEnvironmentStub : IHostEnvironment
{
	public string EnvironmentName { get; set; } = Environments.Production;
	public string ApplicationName { get; set; } = "BeDemo.Api.Tests";
	public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
	public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
