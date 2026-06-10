using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Models;
using BeDemo.Api.Data;
using BeDemo.Api.Models.Requests.Users;
using BeDemo.Api.Security;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ILogger<UsersController> _logger;
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	public UsersController(
		UserManager<ApplicationUser> userManager,
		ILogger<UsersController> logger,
		ApplicationDbContext context,
		IFaceScopeContext faceScope,
		IAccessEvaluator access)
	{
		_userManager = userManager;
		_logger = logger;
		_context = context;
		_faceScope = faceScope;
		_access = access;
	}

	/// <summary>Platform operators (admin face + global admin JWT) — see <see cref="IAccessEvaluator"/>.</summary>
	private bool CanManageAllFaces() => _access.CanManageAllFaces(User);

	/// <summary>
	/// GET /api/users
	/// Get list of users with optional pagination and search.
	/// Query params: page (1-based), pageSize (default 10), search (filter by name or email),
	/// forAddFriend (when true, exclude current user, friends, and pending request users).
	/// </summary>
	/// <remarks>
	/// ACL A19 (social / directory): mutual <see cref="UserBlock"/> rows hide users both ways; tenant callers without
	/// <c>CanManageAllFaces</c> only see users who have <see cref="UserFaceProfile"/> for the scoped face — no cross-face directory leakage.
	/// </remarks>
	[HttpGet]
	public async Task<IActionResult> GetUsers([FromQuery] GetUsersQuery listQuery)
	{
		try
		{
			var page = listQuery.Page;
			var pageSize = listQuery.PageSize;
			var search = listQuery.Search;
			var forAddFriend = listQuery.ForAddFriend;

			var currentUserId = _userManager.GetUserId(User);
			if (string.IsNullOrEmpty(currentUserId))
			{
				return Unauthorized();
			}

			var query = _context.Users.AsQueryable();

			// Exclude users who blocked current user or were blocked by current user
			var blockedByMe = _context.UserBlocks
				.Where(b => b.BlockerId == currentUserId)
				.Select(b => b.BlockedId);
			var blockedMe = _context.UserBlocks
				.Where(b => b.BlockedId == currentUserId)
				.Select(b => b.BlockerId);
			query = query.Where(u => !blockedByMe.Contains(u.Id) && !blockedMe.Contains(u.Id));

			if (forAddFriend)
			{
				var friendIds = await _context.Friendships
					.Where(f => f.UserId == currentUserId || f.FriendId == currentUserId)
					.Select(f => f.UserId == currentUserId ? f.FriendId : f.UserId)
					.ToListAsync();
				var requestSenderIds = await _context.FriendRequests
					.Where(r => r.ReceiverId == currentUserId && r.Status == BeDemo.Api.Models.FriendRequestStatus.Pending)
					.Select(r => r.SenderId)
					.ToListAsync();
				var requestReceiverIds = await _context.FriendRequests
					.Where(r => r.SenderId == currentUserId && r.Status == BeDemo.Api.Models.FriendRequestStatus.Pending)
					.Select(r => r.ReceiverId)
					.ToListAsync();
				var excludeIds = new[] { currentUserId }
					.Concat(friendIds)
					.Concat(requestSenderIds)
					.Concat(requestReceiverIds)
					.Distinct()
					.ToList();
				query = query.Where(u => !excludeIds.Contains(u.Id));
			}

			if (!string.IsNullOrWhiteSpace(search))
			{
				var pattern = $"%{search.Trim()}%";
				query = query.Where(u =>
					(u.FirstName != null && EF.Functions.ILike(u.FirstName, pattern)) ||
					(u.LastName != null && EF.Functions.ILike(u.LastName, pattern)) ||
					(u.Email != null && EF.Functions.ILike(u.Email, pattern)));
			}

			// Tenant directory: only users who have a UserFaceProfile row for this face (community members).
			// Admin scope + global Admin JWT: full user list for moderation.
			if (!CanManageAllFaces())
			{
				var scopeFaceId = _faceScope.FaceId;
				query = query.Where(u =>
					_context.UserProfiles.Any(up =>
						up.UserId == u.Id &&
						_context.UserFaceProfiles.Any(ufp =>
							ufp.UserProfileId == up.Id && ufp.FaceId == scopeFaceId)));
			}

			// Server-driven admin table: count → clamp page when filters shrink result set → sort → page slice.
			var totalCount = await query.CountAsync();
			var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
			page = clampedPage;

			var users = await ListSortApplicators
				.ApplyUsersSort(query, listQuery.SortBy, listQuery.SortDir)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			var userDtos = users.Select(u => new
			{
				id = u.Id,
				email = u.Email,
				firstName = u.FirstName,
				lastName = u.LastName,
				createdAt = u.CreatedAt,
			}).ToList();

			_logger.LogInformation("Retrieved {Count} users (page {Page}, total {Total})", userDtos.Count, page, totalCount);

			return Ok(new
			{
				items = userDtos,
				totalCount,
				page,
				pageSize,
				totalPages,
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving users");
			return StatusCode(500, new { error = "An error occurred while retrieving users" });
		}
	}

	/// <summary>
	/// GET /api/users/{id}
	/// Get user by ID
	/// </summary>
	[HttpGet("{id}")]
	public async Task<IActionResult> GetUser(string id)
	{
		try
		{
			var user = await _userManager.FindByIdAsync(id);

			if (user == null)
			{
				_logger.LogWarning("User not found: {UserId}", id);
				return NotFound(new { error = "User not found" });
			}

			if (!CanManageAllFaces())
			{
				var inScope = await _context.UserProfiles
					.AnyAsync(up =>
						up.UserId == id &&
						_context.UserFaceProfiles.Any(ufp =>
							ufp.UserProfileId == up.Id && ufp.FaceId == _faceScope.FaceId));
				if (!inScope)
					return NotFound(new { error = "User not found" });
			}

			var userDto = new
			{
				id = user.Id,
				email = user.Email,
				firstName = user.FirstName,
				lastName = user.LastName,
				createdAt = user.CreatedAt,
			};

			_logger.LogInformation("Retrieved user: {UserId}", id);
			return Ok(userDto);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving user: {UserId}", id);
			return StatusCode(500, new { error = "An error occurred while retrieving user" });
		}
	}

	/// <summary>
	/// POST /api/users
	/// Create a new user
	/// </summary>
	// Backend-refactor X5/X6: operator-only mutation gated by the ManageAllFaces policy (method-level, since list/get
	// keep a per-face visibility branch on CanManageAllFaces() that is NOT a blanket gate). Authorization now runs
	// before model validation — the conventional, more-secure order: an unauthorized caller is refused (403) without
	// learning whether the body was well-formed (previously a malformed body from an unauthorized caller returned 400).
	[HttpPost]
	[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]
	public async Task<IActionResult> CreateUser([FromBody] CreateUserModel model)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		try
		{
			// Get USER (global) role - default for new users
			var userRole = await _context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.GlobalRoleNames.User);
			if (userRole == null)
			{
				_logger.LogError("USER role not found. Please ensure UserRoles are seeded.");
				return StatusCode(500, new { error = "System configuration error: USER role not found" });
			}

			var user = new ApplicationUser
			{
				UserName = model.Email,
				Email = model.Email,
				FirstName = model.FirstName,
				LastName = model.LastName,
				UserRoleId = userRole.Id // Assign default USER role
			};

			var result = await _userManager.CreateAsync(user, model.Password);

			if (result.Succeeded)
			{
				var userDto = new
				{
					id = user.Id,
					email = user.Email,
					firstName = user.FirstName,
					lastName = user.LastName,
					createdAt = user.CreatedAt,
				};

				_logger.LogInformation("User created: {UserId}", user.Id);
				return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
			}

			_logger.LogWarning(
				"User creation failed ({EmailHint}), Errors: {Errors}",
				PiiLogRedaction.FormatEmailForLog(model.Email),
				string.Join(", ", result.Errors.Select(e => e.Description)));
			return BadRequest(result.Errors);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error creating user");
			return StatusCode(500, new { error = "An error occurred while creating user" });
		}
	}

	/// <summary>
	/// PUT /api/users/{id}
	/// Update user by ID
	/// </summary>
	[HttpPut("{id}")]
	[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]
	public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserModel model)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		try
		{
			var user = await _userManager.FindByIdAsync(id);

			if (user == null)
			{
				_logger.LogWarning("User not found for update: {UserId}", id);
				return NotFound(new { error = "User not found" });
			}

			// Update user properties
			if (model.Email != null)
			{
				user.Email = model.Email;
				user.UserName = model.Email;
			}
			if (model.FirstName != null)
			{
				user.FirstName = model.FirstName;
			}
			if (model.LastName != null)
			{
				user.LastName = model.LastName;
			}

			// Update password if provided
			IdentityResult result;
			if (!string.IsNullOrEmpty(model.Password))
			{
				var token = await _userManager.GeneratePasswordResetTokenAsync(user);
				result = await _userManager.ResetPasswordAsync(user, token, model.Password);

				if (!result.Succeeded)
				{
					_logger.LogWarning("Password update failed for user: {UserId}, Errors: {Errors}", id, string.Join(", ", result.Errors.Select(e => e.Description)));
					return BadRequest(result.Errors);
				}

				// J6: AccessTokenVersion + refresh revocation run in ApplicationDbContext.SaveChanges (ResetPasswordAsync saves).
			}

			result = await _userManager.UpdateAsync(user);

			if (result.Succeeded)
			{
				var userDto = new
				{
					id = user.Id,
					email = user.Email,
					firstName = user.FirstName,
					lastName = user.LastName,
					createdAt = user.CreatedAt,
				};

				_logger.LogInformation("User updated: {UserId}", id);
				return Ok(userDto);
			}

			_logger.LogWarning("User update failed: {UserId}, Errors: {Errors}", id, string.Join(", ", result.Errors.Select(e => e.Description)));
			return BadRequest(result.Errors);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating user: {UserId}", id);
			return StatusCode(500, new { error = "An error occurred while updating user" });
		}
	}
}
