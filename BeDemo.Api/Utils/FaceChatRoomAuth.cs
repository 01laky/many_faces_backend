using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Utils;

public static class FaceChatRoomAuth
{
    public static async Task<bool> IsHostInFaceAsync(
        ApplicationDbContext context,
        string userId,
        int faceId,
        CancellationToken cancellationToken = default)
    {
        var ufr = await context.UserFaceRoles
            .AsNoTracking()
            .Include(x => x.UserRole)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.FaceId == faceId, cancellationToken);

        return ufr?.UserRole != null && FaceRoleParticipation.IsHostFaceRole(ufr.UserRole.Name);
    }

    public static async Task<bool> IsGlobalAdminAsync(ApplicationDbContext context, ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var role = await context.UserRoles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == user.UserRoleId, cancellationToken);
        if (role == null)
            return false;
        return role.Name is UserRole.GlobalRoleNames.Admin or UserRole.GlobalRoleNames.SuperAdmin;
    }
}
