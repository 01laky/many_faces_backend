using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>
/// Post-create side effects shared by legacy register and email-code <c>complete</c> (profiles, face links).
/// </summary>
public sealed class UserRegistrationProvisioner : IUserRegistrationProvisioner
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserRegistrationProvisioner> _logger;

    public UserRegistrationProvisioner(ApplicationDbContext context, ILogger<UserRegistrationProvisioner> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserRegistrationProvisionResult> ProvisionNewUserAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var userProfile = new UserProfile
        {
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
        };
        _context.UserProfiles.Add(userProfile);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var faces = await _context.Faces.ToListAsync(cancellationToken).ConfigureAwait(false);
        var userFaceProfiles = faces.Select(face => new UserFaceProfile
        {
            UserProfileId = userProfile.Id,
            FaceId = face.Id,
            IsActive = false,
            Visited = false,
            FaceRoleIntroCompleted = false,
            CreatedAt = DateTime.UtcNow,
        }).ToList();

        if (userFaceProfiles.Count > 0)
        {
            _context.UserFaceProfiles.AddRange(userFaceProfiles);
            var faceHostRole = await _context.UserRoles
                .FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceHost, cancellationToken)
                .ConfigureAwait(false);
            if (faceHostRole != null)
            {
                var userFaceRoles = faces.Select(face => new UserFaceRole
                {
                    UserId = user.Id,
                    FaceId = face.Id,
                    UserRoleId = faceHostRole.Id,
                    CreatedAt = DateTime.UtcNow,
                }).ToList();
                _context.UserFaceRoles.AddRange(userFaceRoles);
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Created {Count} UserFaceProfile(s) for user: {Email}",
                userFaceProfiles.Count,
                user.Email);
        }

        return new UserRegistrationProvisionResult(userProfile.Id, userFaceProfiles.Count);
    }
}
