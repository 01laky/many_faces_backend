using BeDemo.Api.Models;
using BeDemo.Api.Services.Search;
using BeDemo.Api.Tests.TestDoubles;
using FluentAssertions;
using ManyFaces.Search.V1;
using Xunit;

namespace BeDemo.Api.Tests.Services.Search;

/// <summary>
/// Characterization tests for the defense-in-depth ACL filter <see cref="SearchHitAclFilter"/> (backend-refactor
/// §5.2, a tenant-isolation boundary with 0 tests): ES hits must be re-checked against PostgreSQL so soft-removed,
/// inactive, banned, or nonexistent rows never surface in admin search.
/// </summary>
public sealed class SearchHitAclFilterTests
{
	private static AutocompleteHit Hit(string type, string id) => new() { DocumentType = type, EntityId = id };

	[Fact]
	public async Task Null_or_empty_type_or_id_is_not_visible()
	{
		await using var db = InMemoryDb.Fresh();
		var f = new SearchHitAclFilter(db);
		(await f.IsVisibleAsync(Hit("", "1"), default)).Should().BeFalse();
		(await f.IsVisibleAsync(Hit(SearchDocumentTypes.Album, ""), default)).Should().BeFalse();
	}

	[Fact]
	public async Task Unknown_document_type_is_not_visible()
	{
		await using var db = InMemoryDb.Fresh();
		(await new SearchHitAclFilter(db).IsVisibleAsync(Hit("ghost", "1"), default)).Should().BeFalse();
	}

	[Fact]
	public async Task Soft_removed_album_is_hidden_while_a_live_album_is_visible()
	{
		await using var db = InMemoryDb.Fresh();
		db.Albums.Add(new Album { Id = 1, CreatorId = "c1", RemovedAtUtc = null });
		db.Albums.Add(new Album { Id = 2, CreatorId = "c1", RemovedAtUtc = DateTime.UtcNow });
		await db.SaveChangesAsync();
		var f = new SearchHitAclFilter(db);
		(await f.IsVisibleAsync(Hit(SearchDocumentTypes.Album, "1"), default)).Should().BeTrue();
		(await f.IsVisibleAsync(Hit(SearchDocumentTypes.Album, "2"), default)).Should().BeFalse("soft-removed content must not leak via search");
		(await f.IsVisibleAsync(Hit(SearchDocumentTypes.Album, "999"), default)).Should().BeFalse("nonexistent id");
	}

	[Fact]
	public async Task Inactive_face_profile_is_hidden()
	{
		await using var db = InMemoryDb.Fresh();
		db.UserFaceProfiles.Add(new UserFaceProfile { Id = 1, IsActive = true, UserProfileId = 1, FaceId = 1, CreatedAt = DateTime.UtcNow });
		db.UserFaceProfiles.Add(new UserFaceProfile { Id = 2, IsActive = false, UserProfileId = 1, FaceId = 1, CreatedAt = DateTime.UtcNow });
		await db.SaveChangesAsync();
		var f = new SearchHitAclFilter(db);
		(await f.IsVisibleAsync(Hit(SearchDocumentTypes.FaceProfile, "1"), default)).Should().BeTrue();
		(await f.IsVisibleAsync(Hit(SearchDocumentTypes.FaceProfile, "2"), default)).Should().BeFalse();
	}

	[Fact]
	public async Task Banned_or_emailless_user_is_hidden()
	{
		await using var db = InMemoryDb.Fresh();
		db.Users.Add(new ApplicationUser { Id = "ok", Email = "a@b.c", UserName = "ok" });
		db.Users.Add(new ApplicationUser { Id = "noemail", Email = null, UserName = "noemail" });
		db.Users.Add(new ApplicationUser { Id = "banned", Email = "x@y.z", UserName = "banned", LockoutEnd = DateTimeOffset.UtcNow.AddYears(100) });
		await db.SaveChangesAsync();
		var f = new SearchHitAclFilter(db);
		(await f.IsVisibleAsync(Hit(SearchDocumentTypes.User, "ok"), default)).Should().BeTrue();
		(await f.IsVisibleAsync(Hit(SearchDocumentTypes.User, "noemail"), default)).Should().BeFalse();
		(await f.IsVisibleAsync(Hit(SearchDocumentTypes.User, "banned"), default)).Should().BeFalse("globally banned users must not surface in search");
		(await f.IsVisibleAsync(Hit(SearchDocumentTypes.User, "missing"), default)).Should().BeFalse();
	}
}
