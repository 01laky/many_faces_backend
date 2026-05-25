using BeDemo.Api.Models.DTOs;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BeDemo.Api.Validation.Filters;

/// <summary>
/// Maps FluentValidation failures on <see cref="OAuth2TokenRequest"/> to <see cref="OAuth2ErrorResponse"/>
/// (invalid_request) instead of <see cref="ValidationProblemDetails"/> (§6).
/// </summary>
public sealed class OAuth2FluentValidationFilter : IAsyncActionFilter
{
	public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
	{
		foreach (var arg in context.ActionArguments.Values)
		{
			if (arg is not OAuth2TokenRequest request)
				continue;

			var validator = context.HttpContext.RequestServices.GetService<IValidator<OAuth2TokenRequest>>();
			if (validator is null)
			{
				await next();
				return;
			}

			ValidationResult result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
			if (result.IsValid)
				continue;

			var description = string.Join("; ", result.Errors.Select(e => e.ErrorMessage));
			context.Result = new BadRequestObjectResult(new OAuth2ErrorResponse
			{
				Error = "invalid_request",
				ErrorDescription = string.IsNullOrWhiteSpace(description) ? "Validation failed." : description,
			});
			return;
		}

		await next();
	}
}
