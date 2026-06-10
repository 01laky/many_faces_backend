using Microsoft.AspNetCore.DataProtection;

namespace BeDemo.Api.Services.OperatorPush;

/// <summary>
/// Encrypts operator push secrets at rest using ASP.NET Data Protection. Logic lives in
/// <see cref="Services.OperatorSecretProtectorBase"/>; this type only fixes the (unchanged) push purpose string.
/// </summary>
public sealed class OperatorPushSecretProtector : Services.OperatorSecretProtectorBase, IOperatorPushSecretProtector
{
	public OperatorPushSecretProtector(IDataProtectionProvider provider)
		: base(provider, "BeDemo.Api.OperatorPushSystemSettings.v1")
	{
	}
}
