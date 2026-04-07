using BeDemo.Api.Models;

namespace BeDemo.Api.Utils;

public static class FaceRoleParticipation
{
    public static bool IsHostFaceRole(string? roleName) =>
        string.Equals(roleName, UserRole.FaceRoleNames.FaceHost, StringComparison.Ordinal);

    /// <summary>Non-host roles are "active" in the face directory sense.</summary>
    public static bool IsActiveForFaceRoleName(string? roleName) => !IsHostFaceRole(roleName);
}
