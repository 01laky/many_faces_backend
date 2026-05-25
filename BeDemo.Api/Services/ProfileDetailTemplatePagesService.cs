using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.ProfileDetail;
using BeDemo.Api.Validation.Pages;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

public sealed class ProfileDetailTemplatePagesService : IProfileDetailTemplatePagesService
{
	private readonly ApplicationDbContext _context;

	public ProfileDetailTemplatePagesService(ApplicationDbContext context)
	{
		_context = context;
	}

	public async Task<int?> GetProfileDetailPageTypeIdAsync(CancellationToken cancellationToken = default)
	{
		var pt = await _context.PageTypes.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Index == ProfileDetailGridDefaults.PageTypeIndex, cancellationToken);
		return pt?.Id;
	}

	public string? ValidateGridSchemaJson(string? gridSchemaJson) =>
		ProfileDetailGridSchemaValidator.Validate(gridSchemaJson);

	public async Task<int> EnsureAllFacesAsync(CancellationToken cancellationToken = default)
	{
		var pageTypeId = await GetProfileDetailPageTypeIdAsync(cancellationToken);
		if (pageTypeId == null)
			return 0;

		var faceIds = await _context.Faces
			.AsNoTracking()
			.Select(f => f.Id)
			.ToListAsync(cancellationToken);
		var faceIdsWithTemplate = await _context.Pages
			.AsNoTracking()
			.Where(p => p.PageTypeId == pageTypeId.Value)
			.Select(p => p.FaceId)
			.ToHashSetAsync(cancellationToken);

		var created = 0;
		foreach (var faceId in faceIds)
		{
			// Seeding may run on every startup. Keep it idempotent by calculating the missing
			// face set once, then adding only the template rows that are absent.
			if (faceIdsWithTemplate.Contains(faceId))
				continue;

			AddProfileDetailTemplatePage(faceId, pageTypeId.Value);
			created++;
		}

		if (created > 0)
			await _context.SaveChangesAsync(cancellationToken);

		return created;
	}

	public async Task<bool> EnsureForFaceAsync(int faceId, CancellationToken cancellationToken = default)
	{
		var pageTypeId = await GetProfileDetailPageTypeIdAsync(cancellationToken);
		if (pageTypeId == null)
			return false;

		var added = await EnsureForFaceInternalAsync(faceId, pageTypeId.Value, cancellationToken);
		if (added)
			await _context.SaveChangesAsync(cancellationToken);
		return added;
	}

	private async Task<bool> EnsureForFaceInternalAsync(int faceId, int pageTypeId, CancellationToken cancellationToken)
	{
		var exists = await _context.Pages.AnyAsync(
			p => p.FaceId == faceId && p.PageTypeId == pageTypeId,
			cancellationToken);

		if (exists)
			return false;

		AddProfileDetailTemplatePage(faceId, pageTypeId);
		return true;
	}

	private void AddProfileDetailTemplatePage(int faceId, int pageTypeId)
	{
		// The default template is intentionally identical for every face. Admin users can later
		// edit GridSchema per face without changing the global seed definition.
		_context.Pages.Add(new Page
		{
			FaceId = faceId,
			PageTypeId = pageTypeId,
			Name = ProfileDetailGridDefaults.TemplatePageName,
			Path = ProfileDetailGridDefaults.TemplatePagePath,
			Index = ProfileDetailGridDefaults.TemplatePageSortIndex,
			GridSchema = ProfileDetailGridDefaults.DefaultGridSchemaJson,
			CreatedAt = DateTime.UtcNow,
		});
	}
}
