using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class RegistrationInviteCleanupHostedServiceTests
{
    [Fact]
    public async Task RunCleanupAsync_ShouldDeleteExpiredRows()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.Configure<RegistrationInviteOptions>(o => o.ConsumedRetentionDays = 7);
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.RegistrationInvites.Add(new RegistrationInvite
            {
                Id = Guid.NewGuid(),
                Email = "exp@test.com",
                NormalizedEmail = "EXP@TEST.COM",
                LinkHash = "hash-expired",
                CodeHash = "abc",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                Locale = "en",
            });
            await ctx.SaveChangesAsync();
        }

        var sut = new RegistrationInviteCleanupHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<RegistrationInviteOptions>>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RegistrationInviteCleanupHostedService>.Instance);

        await sut.RunCleanupAsync(CancellationToken.None);

        await using var verifyScope = provider.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verify.RegistrationInvites.CountAsync()).Should().Be(0);
    }
}
