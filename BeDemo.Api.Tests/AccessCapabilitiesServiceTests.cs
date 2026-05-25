using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Security;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

public class AccessCapabilitiesServiceTests
{
	private static Mock<IFaceScopeContext> Scope(int faceId, string index, bool adminScope)
	{
		var m = new Mock<IFaceScopeContext>();
		m.SetupGet(x => x.IsAvailable).Returns(true);
		m.SetupGet(x => x.FaceId).Returns(faceId);
		m.SetupGet(x => x.FaceIndex).Returns(index);
		m.SetupGet(x => x.IsAdminFaceScope).Returns(adminScope);
		return m;
	}

	private static ClaimsPrincipal P(params string[] roles)
	{
		var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
		return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
	}

	[Fact]
	public async Task TenantPublic_IncludesSessionSelfServiceFaceMember_WhenFaceRoleExists()
	{
		var dbName = $"ac_{Guid.NewGuid():N}";
		var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options;
		await using var db = new ApplicationDbContext(options);

		db.UserRoles.AddRange(
			new UserRole { Id = 101, Name = UserRole.GlobalRoleNames.User, Scope = RoleScope.Global },
			new UserRole { Id = 102, Name = UserRole.FaceRoleNames.FaceUser, Scope = RoleScope.Face });
		db.Users.Add(new ApplicationUser
		{
			Id = "tenant-1",
			UserName = "t@test.com",
			Email = "t@test.com",
			UserRoleId = 101,
		});
		db.UserFaceRoles.Add(new UserFaceRole
		{
			UserId = "tenant-1",
			FaceId = 50,
			UserRoleId = 102,
			CreatedAt = DateTime.UtcNow,
		});
		await db.SaveChangesAsync();

		var svc = new AccessCapabilitiesService(db, Scope(50, "public", adminScope: false).Object);
		var dto = await svc.GetCapabilitiesAsync("tenant-1", P(UserRole.GlobalRoleNames.User));

		dto.GlobalRole.Should().Be(UserRole.GlobalRoleNames.User);
		dto.IsAdminFaceScope.Should().BeFalse();
		dto.MyFaceRoleName.Should().Be(UserRole.FaceRoleNames.FaceUser);
		dto.Permissions.Should().Contain(AclPermissionKeys.TenantSession);
		dto.Permissions.Should().Contain(AclPermissionKeys.FaceRoleSelfService);
		dto.Permissions.Should().Contain(AclPermissionKeys.FaceMember);
		dto.Permissions.Should().NotContain(AclPermissionKeys.PlatformAdmin);
	}

	[Fact]
	public async Task AdminFaceWithGlobalAdmin_ExcludesPlatformPermissions_NotSelfService()
	{
		var dbName = $"ac2_{Guid.NewGuid():N}";
		var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options;
		await using var db = new ApplicationDbContext(options);

		db.UserRoles.Add(new UserRole { Id = 201, Name = UserRole.GlobalRoleNames.Admin, Scope = RoleScope.Global });
		db.Users.Add(new ApplicationUser
		{
			Id = "adm-1",
			UserName = "a@test.com",
			Email = "a@test.com",
			UserRoleId = 201,
		});
		await db.SaveChangesAsync();

		var svc = new AccessCapabilitiesService(db, Scope(88, "admin", adminScope: true).Object);
		var dto = await svc.GetCapabilitiesAsync("adm-1", P(UserRole.GlobalRoleNames.Admin));

		dto.Permissions.Should().NotContain(AclPermissionKeys.PlatformAdmin);
		dto.Permissions.Should().NotContain(AclPermissionKeys.PlatformPagetypeMutate);
		dto.Permissions.Should().NotContain(AclPermissionKeys.PlatformSuper);
		dto.Permissions.Should().Contain(AclPermissionKeys.TenantSession);
		dto.Permissions.Should().NotContain(AclPermissionKeys.FaceRoleSelfService);
	}

	[Fact]
	public async Task SuperAdmin_OnAdminFace_IncludesPlatformSuper()
	{
		var dbName = $"ac3_{Guid.NewGuid():N}";
		var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options;
		await using var db = new ApplicationDbContext(options);

		db.UserRoles.Add(new UserRole { Id = 301, Name = UserRole.GlobalRoleNames.SuperAdmin, Scope = RoleScope.Global });
		db.Users.Add(new ApplicationUser
		{
			Id = "sup-1",
			UserName = "s@test.com",
			Email = "s@test.com",
			UserRoleId = 301,
		});
		await db.SaveChangesAsync();

		var svc = new AccessCapabilitiesService(db, Scope(88, "admin", adminScope: true).Object);
		var dto = await svc.GetCapabilitiesAsync("sup-1", P(UserRole.GlobalRoleNames.SuperAdmin));

		dto.Permissions.Should().Contain(AclPermissionKeys.PlatformSuper);
		dto.Permissions.Should().Contain(AclPermissionKeys.PlatformAdmin);
	}

	[Fact]
	public async Task PermissionsList_IsOrderedAndUnique()
	{
		var dbName = $"ac4_{Guid.NewGuid():N}";
		var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options;
		await using var db = new ApplicationDbContext(options);

		db.UserRoles.Add(new UserRole { Id = 401, Name = UserRole.GlobalRoleNames.SuperAdmin, Scope = RoleScope.Global });
		db.Users.Add(new ApplicationUser
		{
			Id = "sup-2",
			UserName = "s2@test.com",
			Email = "s2@test.com",
			UserRoleId = 401,
		});
		await db.SaveChangesAsync();

		var svc = new AccessCapabilitiesService(db, Scope(88, "admin", adminScope: true).Object);
		var dto = await svc.GetCapabilitiesAsync("sup-2", P(UserRole.GlobalRoleNames.SuperAdmin));

		dto.Permissions.Should().Equal(dto.Permissions.OrderBy(x => x, StringComparer.Ordinal));
		dto.Permissions.Should().OnlyHaveUniqueItems();
	}
}
