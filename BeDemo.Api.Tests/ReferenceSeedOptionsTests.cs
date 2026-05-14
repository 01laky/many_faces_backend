using BeDemo.Api.Scripts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;

namespace BeDemo.Api.Tests;

public class ReferenceSeedOptionsTests
{
    [Fact]
    public void ShouldSeedReferenceDataViaApi_Testing_AlwaysTrue()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Testing");
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ReferenceSeedOptions.AssumeExternalSqlReferenceAppliedKey] = "true",
        }!).Build();

        Assert.True(ReferenceSeedOptions.ShouldSeedReferenceDataViaApi(env.Object, cfg));
    }

    [Fact]
    public void ShouldSeedReferenceDataViaApi_Development_FalseWhenAssumeExternalSql()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ReferenceSeedOptions.AssumeExternalSqlReferenceAppliedKey] = "true",
        }!).Build();

        Assert.False(ReferenceSeedOptions.ShouldSeedReferenceDataViaApi(env.Object, cfg));
    }

    [Fact]
    public void ShouldSeedReferenceDataViaApi_Development_TrueWhenNotAssumed()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();

        Assert.True(ReferenceSeedOptions.ShouldSeedReferenceDataViaApi(env.Object, cfg));
    }

    [Fact]
    public void ShouldSeedReferenceDataViaApi_Production_FalseWhenAssumeExternalSql()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ReferenceSeedOptions.AssumeExternalSqlReferenceAppliedKey] = "true",
        }!).Build();

        Assert.False(ReferenceSeedOptions.ShouldSeedReferenceDataViaApi(env.Object, cfg));
    }

    [Fact]
    public void ShouldSeedReferenceDataViaApi_Production_TrueWhenNotAssumed()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();

        Assert.True(ReferenceSeedOptions.ShouldSeedReferenceDataViaApi(env.Object, cfg));
    }

    [Fact]
    public void AssumeExternalSqlReferenceApplied_MissingKey_ReturnsFalse()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
        Assert.False(ReferenceSeedOptions.AssumeExternalSqlReferenceApplied(cfg));
    }

    [Fact]
    public void AssumeExternalSqlReferenceApplied_FalseString_ReturnsFalse()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ReferenceSeedOptions.AssumeExternalSqlReferenceAppliedKey] = "false",
        }!).Build();

        Assert.False(ReferenceSeedOptions.AssumeExternalSqlReferenceApplied(cfg));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public void AssumeExternalSqlReferenceApplied_TrueStrings_ReturnTrue(string value)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ReferenceSeedOptions.AssumeExternalSqlReferenceAppliedKey] = value,
        }!).Build();

        Assert.True(ReferenceSeedOptions.AssumeExternalSqlReferenceApplied(cfg));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData("yes")]
    public void AssumeExternalSqlReferenceApplied_InvalidBooleanString_Throws(string value)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ReferenceSeedOptions.AssumeExternalSqlReferenceAppliedKey] = value,
        }!).Build();

        Assert.Throws<InvalidOperationException>(() => ReferenceSeedOptions.AssumeExternalSqlReferenceApplied(cfg));
    }
}
