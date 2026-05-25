using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BeDemo.Api.Swagger;

/// <summary>
/// ACL A23 / G15: mark secured controller actions with OpenAPI <c>security: Bearer</c> so generated clients send JWT.
/// </summary>
internal sealed class BearerAuthOperationFilter : IOperationFilter
{
	public void Apply(OpenApiOperation operation, OperationFilterContext context)
	{
		var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
		if (metadata.Count > 0)
		{
			if (metadata.Any(m => m is IAllowAnonymous))
				return;
			if (!metadata.Any(m => m is AuthorizeAttribute))
				return;
		}
		else
		{
			var method = context.MethodInfo;
			if (method.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any())
				return;

			var hasAuthorize = method.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();
			if (!hasAuthorize && context.ApiDescription.ActionDescriptor is ControllerActionDescriptor cad)
				hasAuthorize = cad.ControllerTypeInfo.AsType().GetCustomAttributes(typeof(AuthorizeAttribute), true).Length > 0;

			if (!hasAuthorize)
				return;
		}

		operation.Security ??= new List<OpenApiSecurityRequirement>();
		operation.Security.Add(new OpenApiSecurityRequirement
		{
			[new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
		});
	}
}
