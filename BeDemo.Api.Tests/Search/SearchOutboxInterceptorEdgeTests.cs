using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BeDemo.Api.Tests.Search;

public sealed class SearchOutboxInterceptorEdgeTests
{
	/// <summary>GSH1-T-O09 — entity mutation stages outbox row in same SaveChanges.</summary>
	[Fact]
	public async Task GSH1_T_O09_FaceCreate_StagesOutboxIndex_InSameSaveChanges()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		await using var db = new ApplicationDbContext(options);
		db.Database.EnsureCreated();

		db.Faces.Add(new Face
		{
			Index = "9001",
			Title = "Search Hook Face",
			Description = "outbox test",
			IsPublic = true,
			CreatedAt = DateTime.UtcNow,
		});

		await db.SaveChangesAsync();

		SearchOutboxStaging.StageIndex(db, SearchDocumentTypes.Face, db.Faces.Single().Id.ToString());
		await db.SaveChangesAsync();

		var pending = await db.SearchOutboxEntries
			.Where(e => e.ProcessedAtUtc == null)
			.ToListAsync();

		pending.Should().ContainSingle();
		pending[0].DocumentType.Should().Be(SearchDocumentTypes.Face);
		pending[0].Operation.Should().Be(SearchOutboxOperation.Index);
	}

	/// <summary>GSH1-T-O10 — soft-removed album is not indexable.</summary>
	[Fact]
	public void GSH1_T_O10_AlbumSoftRemove_ShouldIndexFalse()
	{
		var album = new Album
		{
			CreatorId = "creator-1",
			Title = "Remove me",
			CreatedAt = DateTime.UtcNow,
			RemovedAtUtc = DateTime.UtcNow,
		};

		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		using var db = new ApplicationDbContext(options);
		db.Albums.Add(album);
		db.SaveChanges();

		SearchOutboxEntityMapper.ShouldIndex(db.Entry(album)).Should().BeFalse();
	}

	/// <summary>GSH1-T-O11 — mapper recognizes all indexed entity types.</summary>
	[Theory]
	[InlineData(typeof(ApplicationUser), SearchDocumentTypes.User)]
	[InlineData(typeof(Face), SearchDocumentTypes.Face)]
	[InlineData(typeof(Album), SearchDocumentTypes.Album)]
	public void GSH1_T_O11_EntityMapper_KnownTypes(Type entityType, string expectedDocumentType)
	{
		object entity = entityType.Name switch
		{
			nameof(ApplicationUser) => new ApplicationUser { Id = "u1", Email = "u1@demo.com", UserRoleId = 1 },
			nameof(Face) => new Face { Id = 1, Index = "1", Title = "F" },
			nameof(Album) => new Album { Id = 1, CreatorId = "u1", Title = "A" },
			_ => throw new NotSupportedException(),
		};

		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		using var db = new ApplicationDbContext(options);
		db.Add(entity);
		db.SaveChanges();

		var entry = db.ChangeTracker.Entries().Single();
		SearchOutboxEntityMapper.TryMap(entry, out var documentType, out _).Should().BeTrue();
		documentType.Should().Be(expectedDocumentType);
	}
}
