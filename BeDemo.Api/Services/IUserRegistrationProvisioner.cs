using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>Post-create profile and face bootstrap (shared by legacy register and invite complete).</summary>
public interface IUserRegistrationProvisioner
{
    Task<UserRegistrationProvisionResult> ProvisionNewUserAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}

public sealed record UserRegistrationProvisionResult(int ProfileId, int FaceProfileCount);
