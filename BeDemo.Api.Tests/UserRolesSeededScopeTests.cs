using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Tests;

/// <summary>ACL A12: integration seed must keep <see cref="UserRole.Scope"/> consistent with role names (prevents authZ drift).</summary>
public class UserRolesSeededScopeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public UserRolesSeededScopeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task Seeded_global_role_names_have_Global_scope()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		foreach (var name in new[]
				 {
					 UserRole.GlobalRoleNames.SuperAdmin,
					 UserRole.GlobalRoleNames.Admin,
					 UserRole.GlobalRoleNames.User,
					 UserRole.GlobalRoleNames.Host,
				 })
		{
			var row = await db.UserRoles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == name);
			row.Should().NotBeNull($"role {name} missing from seed");
			row!.Scope.Should().Be(RoleScope.Global);
		}
	}

	[Fact]
	public async Task Seeded_face_role_names_have_Face_scope()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		foreach (var name in new[]
				 {
					 UserRole.FaceRoleNames.FaceAdmin,
					 UserRole.FaceRoleNames.FaceUser,
					 UserRole.FaceRoleNames.Inzerent,
					 UserRole.FaceRoleNames.Subscriber,
					 UserRole.FaceRoleNames.FaceHost,
				 })
		{
			var row = await db.UserRoles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == name);
			row.Should().NotBeNull($"role {name} missing from seed");
			row!.Scope.Should().Be(RoleScope.Face);
		}
	}
}
