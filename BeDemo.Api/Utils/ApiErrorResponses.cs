using Microsoft.AspNetCore.Mvc;

namespace BeDemo.Api.Utils;

/// <summary>Centralized legacy <c>{ error }</c> JSON payloads (preserve exact contract for FE/mobile).</summary>
public static class ApiErrorResponses
{
    public static object Error(string message) => new { error = message };

    public static NotFoundObjectResult NotFound(string message) =>
        new(Error(message));

    public static BadRequestObjectResult BadRequest(string message) =>
        new(Error(message));

    public static ConflictObjectResult Conflict(string message) =>
        new(Error(message));

    public static ObjectResult Forbidden(string message) =>
        new(Error(message)) { StatusCode = StatusCodes.Status403Forbidden };

    public static ObjectResult InternalServerError(string message) =>
        new(Error(message)) { StatusCode = StatusCodes.Status500InternalServerError };
}
