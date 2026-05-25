using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Pages;
using BeDemo.Api.ProfileDetail;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagesController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<PagesController> _logger;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	private readonly IProfileDetailTemplatePagesService _profileDetailTemplates;

	public PagesController(
		ApplicationDbContext context,
		ILogger<PagesController> logger,
		IFaceScopeContext faceScope,
		IAccessEvaluator access,
		IProfileDetailTemplatePagesService profileDetailTemplates)
	{
		_context = context;
		_logger = logger;
		_faceScope = faceScope;
		_access = access;
		_profileDetailTemplates = profileDetailTemplates;
	}

	/// <summary>Admin SPA (/admin/) with global Admin JWT may see or move pages across faces.</summary>
	private bool CanManageAllFaces() => _access.CanManageAllFaces(User);

	/// <summary>Returns NotFound when a tenant tries to touch another face's page.</summary>
	private IActionResult? EnsurePageBelongsToScope(Page page)
	{
		if (CanManageAllFaces())
			return null;
		if (page.FaceId != _faceScope.FaceId)
			return NotFound(new { error = "Page not found" });
		return null;
	}

	/// <summary>
	/// GET /api/pages?faceId={faceId}
	/// Get list of pages for a specific face
	/// </summary>
	[HttpGet]
	public async Task<IActionResult> GetPages([FromQuery] GetPagesQuery pagesQuery)
	{
		try
		{
			var faceId = pagesQuery.FaceId;
			var page = pagesQuery.Page;
			var pageSize = pagesQuery.PageSize;
			IQueryable<Page> query = _context.Pages.AsNoTracking();

			if (CanManageAllFaces())
			{
				if (faceId.HasValue)
					query = query.Where(p => p.FaceId == faceId.Value);
			}
			else
			{
				query = query.Where(p => p.FaceId == _faceScope.FaceId);
			}

			if (!string.IsNullOrWhiteSpace(pagesQuery.Search))
			{
				var pattern = $"%{pagesQuery.Search.Trim()}%";
				query = query.Where(p =>
					EF.Functions.ILike(p.Name, pattern) ||
					EF.Functions.ILike(p.Path, pattern) ||
					(p.Description != null && EF.Functions.ILike(p.Description, pattern)));
			}

			var totalCount = await query.CountAsync();
			var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
			page = clampedPage;

			var pages = await ListSortApplicators
				.ApplyPagesSort(query, pagesQuery.SortBy, pagesQuery.SortDir)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			var items = pages.Select(p => new
			{
				id = p.Id,
				faceId = p.FaceId,
				pageTypeId = p.PageTypeId,
				name = p.Name,
				description = p.Description,
				path = p.Path,
				index = p.Index,
				gridSchema = p.GridSchema,
				createdAt = p.CreatedAt,
				updatedAt = p.UpdatedAt,
			}).ToList();

			_logger.LogInformation("Retrieved {Count} pages", items.Count);
			return Ok(ListPaginationHelper.BuildEnvelope(items, page, pageSize, totalCount, totalPages));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving pages");
			return StatusCode(500, new { error = "An error occurred while retrieving pages" });
		}
	}

	/// <summary>
	/// GET /api/pages/{id}
	/// Get page by ID
	/// </summary>
	[HttpGet("{id}")]
	public async Task<IActionResult> GetPage(int id)
	{
		try
		{
			var page = await _context.Pages.FindAsync(id);

			if (page == null)
			{
				_logger.LogWarning("Page not found: {PageId}", id);
				return NotFound(new { error = "Page not found" });
			}

			var gate = EnsurePageBelongsToScope(page);
			if (gate != null)
				return gate;

			var pageDto = new
			{
				id = page.Id,
				faceId = page.FaceId,
				pageTypeId = page.PageTypeId,
				name = page.Name,
				description = page.Description,
				path = page.Path,
				index = page.Index,
				gridSchema = page.GridSchema,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
			};

			_logger.LogInformation("Retrieved page: {PageId}", id);
			return Ok(pageDto);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving page: {PageId}", id);
			return StatusCode(500, new { error = "An error occurred while retrieving page" });
		}
	}

	/// <summary>
	/// POST /api/pages
	/// Create a new page
	/// </summary>
	[HttpPost]
	public async Task<IActionResult> CreatePage([FromBody] CreatePageModel model)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		try
		{
			// Verify that Face exists
			var faceExists = await _context.Faces.AnyAsync(f => f.Id == model.FaceId);
			if (!faceExists)
			{
				_logger.LogWarning("Face not found: {FaceId}", model.FaceId);
				return BadRequest(new { error = "Face not found" });
			}

			if (!CanManageAllFaces() && model.FaceId != _faceScope.FaceId)
				return BadRequest(new { error = "Pages can only be created for the current face scope" });

			var pageType = await _context.PageTypes.AsNoTracking()
				.FirstOrDefaultAsync(pt => pt.Id == model.PageTypeId);
			if (pageType == null)
			{
				_logger.LogWarning("PageType not found: {PageTypeId}", model.PageTypeId);
				return BadRequest(new { error = "PageType not found" });
			}

			if (pageType.Index == ProfileDetailGridDefaults.PageTypeIndex)
			{
				var duplicate = await _context.Pages.AnyAsync(p =>
					p.FaceId == model.FaceId && p.PageTypeId == pageType.Id);
				if (duplicate)
				{
					return Conflict(new { error = "This face already has a member profile layout page" });
				}

				var gridSchema = string.IsNullOrWhiteSpace(model.GridSchema)
					? ProfileDetailGridDefaults.DefaultGridSchemaJson
					: model.GridSchema;
				var validationError = _profileDetailTemplates.ValidateGridSchemaJson(gridSchema);
				if (validationError != null)
					return BadRequest(new { error = validationError });

				var profileTemplatePage = new Page
				{
					FaceId = model.FaceId,
					PageTypeId = model.PageTypeId,
					Name = model.Name,
					Description = model.Description,
					Path = model.Path,
					Index = model.Index,
					GridSchema = gridSchema,
					CreatedAt = DateTime.UtcNow,
				};

				_context.Pages.Add(profileTemplatePage);
				await _context.SaveChangesAsync();

				var createdDto = new
				{
					id = profileTemplatePage.Id,
					faceId = profileTemplatePage.FaceId,
					pageTypeId = profileTemplatePage.PageTypeId,
					name = profileTemplatePage.Name,
					description = profileTemplatePage.Description,
					path = profileTemplatePage.Path,
					index = profileTemplatePage.Index,
					gridSchema = profileTemplatePage.GridSchema,
					createdAt = profileTemplatePage.CreatedAt,
					updatedAt = profileTemplatePage.UpdatedAt,
				};

				_logger.LogInformation("Profile detail template page created: {PageId}", profileTemplatePage.Id);
				return CreatedAtAction(nameof(GetPage), new { id = profileTemplatePage.Id }, createdDto);
			}

			var page = new Page
			{
				FaceId = model.FaceId,
				PageTypeId = model.PageTypeId,
				Name = model.Name,
				Description = model.Description,
				Path = model.Path,
				Index = model.Index,
				GridSchema = model.GridSchema,
				CreatedAt = DateTime.UtcNow,
			};

			_context.Pages.Add(page);
			await _context.SaveChangesAsync();

			var pageDto = new
			{
				id = page.Id,
				faceId = page.FaceId,
				pageTypeId = page.PageTypeId,
				name = page.Name,
				description = page.Description,
				path = page.Path,
				index = page.Index,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
			};

			_logger.LogInformation("Page created: {PageId}", page.Id);
			return CreatedAtAction(nameof(GetPage), new { id = page.Id }, pageDto);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error creating page");
			return StatusCode(500, new { error = "An error occurred while creating page" });
		}
	}

	/// <summary>
	/// PUT /api/pages/{id}
	/// Update page by ID
	/// </summary>
	[HttpPut("{id}")]
	public async Task<IActionResult> UpdatePage(int id, [FromBody] UpdatePageModel model)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		try
		{
			var page = await _context.Pages.FindAsync(id);

			if (page == null)
			{
				_logger.LogWarning("Page not found for update: {PageId}", id);
				return NotFound(new { error = "Page not found" });
			}

			var updateGate = EnsurePageBelongsToScope(page);
			if (updateGate != null)
				return updateGate;

			var currentPageTypeIndex = await _context.PageTypes.AsNoTracking()
				.Where(pt => pt.Id == page.PageTypeId)
				.Select(pt => pt.Index)
				.FirstOrDefaultAsync();

			var effectivePageTypeId = model.PageTypeId ?? page.PageTypeId;
			var effectivePageTypeIndex = model.PageTypeId.HasValue
				? await _context.PageTypes.AsNoTracking()
					.Where(pt => pt.Id == model.PageTypeId.Value)
					.Select(pt => pt.Index)
					.FirstOrDefaultAsync()
				: currentPageTypeIndex;

			if (currentPageTypeIndex == ProfileDetailGridDefaults.PageTypeIndex
				&& effectivePageTypeIndex != ProfileDetailGridDefaults.PageTypeIndex)
			{
				return BadRequest(new { error = "Cannot change page type of the member profile layout template" });
			}

			if (effectivePageTypeIndex == ProfileDetailGridDefaults.PageTypeIndex && model.GridSchema != null)
			{
				var validationError = _profileDetailTemplates.ValidateGridSchemaJson(model.GridSchema);
				if (validationError != null)
					return BadRequest(new { error = validationError });
			}

			// Update page properties
			if (model.Name != null)
			{
				page.Name = model.Name;
			}
			if (model.Description != null)
			{
				page.Description = model.Description;
			}
			if (model.Path != null)
			{
				page.Path = model.Path;
			}
			if (model.Index.HasValue)
			{
				page.Index = model.Index.Value;
			}
			if (model.GridSchema != null)
			{
				page.GridSchema = model.GridSchema;
			}
			if (model.FaceId.HasValue)
			{
				if (!CanManageAllFaces())
					return BadRequest(new { error = "Only admin scope may reassign a page to another face" });

				// Verify that new Face exists
				var faceExists = await _context.Faces.AnyAsync(f => f.Id == model.FaceId.Value);
				if (!faceExists)
				{
					_logger.LogWarning("Face not found: {FaceId}", model.FaceId.Value);
					return BadRequest(new { error = "Face not found" });
				}
				page.FaceId = model.FaceId.Value;
			}
			if (model.PageTypeId.HasValue)
			{
				// Verify that new PageType exists
				var pageTypeExists = await _context.PageTypes.AnyAsync(pt => pt.Id == model.PageTypeId.Value);
				if (!pageTypeExists)
				{
					_logger.LogWarning("PageType not found: {PageTypeId}", model.PageTypeId.Value);
					return BadRequest(new { error = "PageType not found" });
				}
				page.PageTypeId = model.PageTypeId.Value;
			}
			page.UpdatedAt = DateTime.UtcNow;

			await _context.SaveChangesAsync();

			var pageDto = new
			{
				id = page.Id,
				faceId = page.FaceId,
				pageTypeId = page.PageTypeId,
				name = page.Name,
				description = page.Description,
				path = page.Path,
				index = page.Index,
				gridSchema = page.GridSchema,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
			};

			_logger.LogInformation("Page updated: {PageId}", id);
			return Ok(pageDto);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating page: {PageId}", id);
			return StatusCode(500, new { error = "An error occurred while updating page" });
		}
	}

	/// <summary>
	/// DELETE /api/pages/{id}
	/// Delete page by ID
	/// </summary>
	[HttpDelete("{id}")]
	public async Task<IActionResult> DeletePage(int id)
	{
		try
		{
			var page = await _context.Pages.FindAsync(id);

			if (page == null)
			{
				_logger.LogWarning("Page not found for deletion: {PageId}", id);
				return NotFound(new { error = "Page not found" });
			}

			var delGate = EnsurePageBelongsToScope(page);
			if (delGate != null)
				return delGate;

			var pageTypeIndex = await _context.PageTypes.AsNoTracking()
				.Where(pt => pt.Id == page.PageTypeId)
				.Select(pt => pt.Index)
				.FirstOrDefaultAsync();
			if (pageTypeIndex == ProfileDetailGridDefaults.PageTypeIndex)
			{
				return BadRequest(new { error = "The member profile layout template cannot be deleted" });
			}

			_context.Pages.Remove(page);
			await _context.SaveChangesAsync();

			_logger.LogInformation("Page deleted: {PageId}", id);
			return NoContent();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error deleting page: {PageId}", id);
			return StatusCode(500, new { error = "An error occurred while deleting page" });
		}
	}

	/// <summary>
	/// GET /api/pages/{pageId}/translations
	/// Get all route translations for a page
	/// </summary>
	[HttpGet("{pageId}/translations")]
	public async Task<IActionResult> GetPageRouteTranslations(int pageId)
	{
		try
		{
			var page = await _context.Pages.FindAsync(pageId);
			if (page == null)
				return NotFound(new { error = "Page not found" });
			var trGate = EnsurePageBelongsToScope(page);
			if (trGate != null)
				return trGate;

			var translations = await _context.PageRouteTranslations
				.Where(t => t.PageId == pageId)
				.OrderBy(t => t.LanguageCode)
				.Select(t => new
				{
					id = t.Id,
					pageId = t.PageId,
					languageCode = t.LanguageCode,
					translatedRoute = t.TranslatedRoute,
					createdAt = t.CreatedAt,
					updatedAt = t.UpdatedAt,
				})
				.ToListAsync();

			return Ok(translations);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving translations for page: {PageId}", pageId);
			return StatusCode(500, new { error = "An error occurred while retrieving page translations" });
		}
	}

	/// <summary>
	/// PUT /api/pages/{pageId}/translations
	/// Upsert route translations for a page (replaces all translations)
	/// </summary>
	[HttpPut("{pageId}/translations")]
	public async Task<IActionResult> UpdatePageRouteTranslations(int pageId, [FromBody] List<PageRouteTranslationModel> models)
	{
		try
		{
			var page = await _context.Pages.FindAsync(pageId);
			if (page == null)
				return NotFound(new { error = "Page not found" });
			var trGate = EnsurePageBelongsToScope(page);
			if (trGate != null)
				return trGate;

			// Get existing translations
			var existing = await _context.PageRouteTranslations
				.Where(t => t.PageId == pageId)
				.ToListAsync();

			// Remove translations not in the new set
			var newLanguageCodes = models.Select(m => m.LanguageCode).ToHashSet();
			var toRemove = existing.Where(e => !newLanguageCodes.Contains(e.LanguageCode)).ToList();
			_context.PageRouteTranslations.RemoveRange(toRemove);

			// Upsert translations
			foreach (var model in models)
			{
				var existingTranslation = existing.FirstOrDefault(e => e.LanguageCode == model.LanguageCode);
				if (existingTranslation != null)
				{
					existingTranslation.TranslatedRoute = model.TranslatedRoute;
					existingTranslation.UpdatedAt = DateTime.UtcNow;
				}
				else
				{
					_context.PageRouteTranslations.Add(new PageRouteTranslation
					{
						PageId = pageId,
						LanguageCode = model.LanguageCode,
						TranslatedRoute = model.TranslatedRoute,
						CreatedAt = DateTime.UtcNow,
					});
				}
			}

			await _context.SaveChangesAsync();

			// Return updated translations
			var translations = await _context.PageRouteTranslations
				.Where(t => t.PageId == pageId)
				.OrderBy(t => t.LanguageCode)
				.Select(t => new
				{
					id = t.Id,
					pageId = t.PageId,
					languageCode = t.LanguageCode,
					translatedRoute = t.TranslatedRoute,
					createdAt = t.CreatedAt,
					updatedAt = t.UpdatedAt,
				})
				.ToListAsync();

			_logger.LogInformation("Updated {Count} translations for page: {PageId}", translations.Count, pageId);
			return Ok(translations);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating translations for page: {PageId}", pageId);
			return StatusCode(500, new { error = "An error occurred while updating page translations" });
		}
	}
}
