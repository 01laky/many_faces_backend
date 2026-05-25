using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.ProfileDetail;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Tests.Services;

public sealed class ProfileDetailTemplatePagesServiceTests
{
	private static ApplicationDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		return new ApplicationDbContext(options);
	}

	[Fact]
	public async Task EnsureAllFacesAsync_is_idempotent()
	{
		await using var context = CreateContext();
		var pageType = new PageType { Index = ProfileDetailGridDefaults.PageTypeIndex };
		context.PageTypes.Add(pageType);
		context.Faces.Add(new Face { Index = "demo", Title = "Demo", IsPublic = true, CreatedAt = DateTime.UtcNow });
		await context.SaveChangesAsync();

		var sut = new ProfileDetailTemplatePagesService(context);
		var first = await sut.EnsureAllFacesAsync();
		var second = await sut.EnsureAllFacesAsync();

		first.Should().Be(1);
		second.Should().Be(0);
		(await context.Pages.CountAsync(p => p.PageTypeId == pageType.Id)).Should().Be(1);
	}

	[Fact]
	public async Task EnsureForFaceAsync_sets_default_grid_schema()
	{
		await using var context = CreateContext();
		var pageType = new PageType { Index = ProfileDetailGridDefaults.PageTypeIndex };
		var face = new Face { Index = "f2", Title = "F2", IsPublic = true, CreatedAt = DateTime.UtcNow };
		context.PageTypes.Add(pageType);
		context.Faces.Add(face);
		await context.SaveChangesAsync();

		var sut = new ProfileDetailTemplatePagesService(context);
		(await sut.EnsureForFaceAsync(face.Id)).Should().BeTrue();

		var page = await context.Pages.SingleAsync(p => p.FaceId == face.Id);
		page.Path.Should().Be(ProfileDetailGridDefaults.TemplatePagePath);
		page.GridSchema.Should().Be(ProfileDetailGridDefaults.DefaultGridSchemaJson);
	}

	[Fact]
	public async Task EnsureAllFacesAsync_returns_zero_when_profile_detail_page_type_is_missing()
	{
		await using var context = CreateContext();
		context.Faces.Add(new Face { Index = "demo", Title = "Demo", IsPublic = true, CreatedAt = DateTime.UtcNow });
		await context.SaveChangesAsync();

		var sut = new ProfileDetailTemplatePagesService(context);

		(await sut.EnsureAllFacesAsync()).Should().Be(0);
		(await context.Pages.CountAsync()).Should().Be(0);
	}

	[Fact]
	public async Task EnsureAllFacesAsync_creates_only_missing_template_pages()
	{
		await using var context = CreateContext();
		var pageType = new PageType { Index = ProfileDetailGridDefaults.PageTypeIndex };
		var existingFace = new Face { Index = "existing", Title = "Existing", IsPublic = true, CreatedAt = DateTime.UtcNow };
		var missingFace = new Face { Index = "missing", Title = "Missing", IsPublic = true, CreatedAt = DateTime.UtcNow };
		context.PageTypes.Add(pageType);
		context.Faces.AddRange(existingFace, missingFace);
		await context.SaveChangesAsync();

		context.Pages.Add(new Page
		{
			FaceId = existingFace.Id,
			PageTypeId = pageType.Id,
			Name = ProfileDetailGridDefaults.TemplatePageName,
			Path = ProfileDetailGridDefaults.TemplatePagePath,
			Index = ProfileDetailGridDefaults.TemplatePageSortIndex,
			GridSchema = ProfileDetailGridDefaults.DefaultGridSchemaJson,
			CreatedAt = DateTime.UtcNow,
		});
		await context.SaveChangesAsync();

		var sut = new ProfileDetailTemplatePagesService(context);

		(await sut.EnsureAllFacesAsync()).Should().Be(1);
		(await context.Pages.CountAsync(p => p.PageTypeId == pageType.Id)).Should().Be(2);
		(await context.Pages.CountAsync(p => p.FaceId == existingFace.Id && p.PageTypeId == pageType.Id)).Should().Be(1);
		(await context.Pages.CountAsync(p => p.FaceId == missingFace.Id && p.PageTypeId == pageType.Id)).Should().Be(1);
	}

	[Fact]
	public async Task EnsureForFaceAsync_returns_false_when_template_already_exists()
	{
		await using var context = CreateContext();
		var pageType = new PageType { Index = ProfileDetailGridDefaults.PageTypeIndex };
		var face = new Face { Index = "f3", Title = "F3", IsPublic = true, CreatedAt = DateTime.UtcNow };
		context.PageTypes.Add(pageType);
		context.Faces.Add(face);
		await context.SaveChangesAsync();

		var sut = new ProfileDetailTemplatePagesService(context);
		(await sut.EnsureForFaceAsync(face.Id)).Should().BeTrue();

		(await sut.EnsureForFaceAsync(face.Id)).Should().BeFalse();
		(await context.Pages.CountAsync(p => p.FaceId == face.Id && p.PageTypeId == pageType.Id)).Should().Be(1);
	}
}
