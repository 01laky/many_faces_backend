using BeDemo.Api.Models.DTOs.Admin;

namespace BeDemo.Api.Services.OperatorMail;

public interface IOperatorMailSettingsProvider
{
    Task<OperatorMailSettingsValues> GetAsync(CancellationToken cancellationToken = default);

    Task<OperatorMailSettingsValues> SetAsync(
        OperatorMailSettingsValues values,
        CancellationToken cancellationToken = default);

    AdminMailSettingsDto ToDto(OperatorMailSettingsValues values);

    void InvalidateCache();
}
