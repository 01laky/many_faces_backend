using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace BeDemo.Api.Services.Auth;

/// <summary>Invalidates ATV memory cache when user session version changes (BE-RP1).</summary>
public sealed class AccessTokenVersionCacheInterceptor : SaveChangesInterceptor
{
	private readonly IMemoryCache _cache;

	public AccessTokenVersionCacheInterceptor(IMemoryCache cache) => _cache = cache;

	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		InvalidateFromContext(eventData.Context);
		return base.SavingChanges(eventData, result);
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		InvalidateFromContext(eventData.Context);
		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	private void InvalidateFromContext(DbContext? context)
	{
		if (context is null)
			return;

		foreach (var entry in context.ChangeTracker.Entries<ApplicationUser>())
		{
			if (entry.State is not (EntityState.Modified or EntityState.Deleted))
				continue;

			var atvProp = entry.Property(u => u.AccessTokenVersion);
			var pwdProp = entry.Property(u => u.PasswordHash);
			var roleProp = entry.Property(u => u.UserRoleId);
			var pwdChanged = pwdProp.IsModified
				|| !string.Equals(pwdProp.OriginalValue as string, pwdProp.CurrentValue as string, StringComparison.Ordinal);
			var roleChanged = roleProp.IsModified || !Equals(roleProp.OriginalValue, roleProp.CurrentValue);
			if (atvProp.IsModified || pwdChanged || roleChanged || entry.State == EntityState.Deleted)
				_cache.Remove(AccessTokenVersionCacheKeys.ForUser(entry.Entity.Id));
		}
	}
}

internal static class AccessTokenVersionCacheKeys
{
	public static string ForUser(string userId) => $"atv:{userId}";
}
