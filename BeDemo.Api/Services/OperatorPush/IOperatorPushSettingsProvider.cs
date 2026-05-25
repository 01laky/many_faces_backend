using BeDemo.Api.Models.DTOs.Admin;

namespace BeDemo.Api.Services.OperatorPush;

public interface IOperatorPushSettingsProvider
{
	Task<OperatorPushSettingsValues> GetAsync(CancellationToken cancellationToken = default);

	Task<OperatorPushSettingsValues> SetAsync(
		OperatorPushSettingsValues values,
		CancellationToken cancellationToken = default);

	AdminPushSettingsDto ToDto(OperatorPushSettingsValues values, PushOptions envOptions);

	void InvalidateCache();
}
