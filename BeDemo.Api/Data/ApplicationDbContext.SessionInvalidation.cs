using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Data;

/// <summary>
/// J6: any save that changes <see cref="ApplicationUser.PasswordHash"/> or <see cref="ApplicationUser.UserRoleId"/> bumps
/// <see cref="ApplicationUser.AccessTokenVersion"/> (at least to <c>original + 1</c>) and revokes active refresh tokens for that user
/// (same transaction as Identity/UserManager saves). UserRoleId also compares original vs current values because the change
/// tracker sometimes omits <c>IsModified</c> on the FK in tests/CI while the value still diverges from the snapshot.
/// </summary>
public partial class ApplicationDbContext
{
    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAccessTokenInvalidationAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ApplyAccessTokenInvalidationAsync(cancellationToken).ConfigureAwait(false);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyAccessTokenInvalidationAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userIds = new HashSet<string>();

        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State != EntityState.Modified)
                continue;

            var pwdProp = entry.Property(u => u.PasswordHash);
            var roleProp = entry.Property(u => u.UserRoleId);

            // Prefer value comparison for UserRoleId: some providers/tests do not always mark FK IsModified
            // even when CurrentValue diverges from the snapshot (J6 / AccessTokenVersionTests).
            var pwdChanged = pwdProp.IsModified
                || !string.Equals(
                    pwdProp.OriginalValue as string,
                    pwdProp.CurrentValue as string,
                    StringComparison.Ordinal);
            var roleChanged = roleProp.IsModified
                || !Equals(roleProp.OriginalValue, roleProp.CurrentValue);

            if (pwdChanged || roleChanged)
            {
                var atvProp = entry.Property(u => u.AccessTokenVersion);
                var floor = atvProp.OriginalValue + 1;
                atvProp.CurrentValue = Math.Max(atvProp.CurrentValue, floor);
                atvProp.IsModified = true;
                userIds.Add(entry.Entity.Id);

                if (pwdChanged)
                    SecurityAuditLogger.LogPasswordChanged(entry.Entity.Id);
                if (roleChanged)
                {
                    SecurityAuditLogger.LogGlobalRoleChanged(
                        entry.Entity.Id,
                        roleProp.OriginalValue,
                        roleProp.CurrentValue);
                }
            }
        }

        if (userIds.Count == 0)
            return;

        var tokens = await OAuthRefreshTokens
            .Where(t => userIds.Contains(t.UserId) && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var t in tokens)
            t.RevokedAtUtc = now;
    }
}
