using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Models;

namespace BeDemo.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<FriendRequest> FriendRequests { get; set; } = null!;
    public DbSet<Friendship> Friendships { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<Face> Faces { get; set; } = null!;
    public DbSet<Page> Pages { get; set; } = null!;
    public DbSet<PageType> PageTypes { get; set; } = null!;
    public DbSet<PageRouteTranslation> PageRouteTranslations { get; set; } = null!;
    public DbSet<UserProfile> UserProfiles { get; set; } = null!;
    public DbSet<UserFaceProfile> UserFaceProfiles { get; set; } = null!;
    public new DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<UserFaceRole> UserFaceRoles { get; set; } = null!;
    public DbSet<UserBlock> UserBlocks { get; set; } = null!;
    public DbSet<UserFollow> UserFollows { get; set; } = null!;
    public DbSet<ComponentType> ComponentTypes { get; set; } = null!;
    public DbSet<DisplayMode> DisplayModes { get; set; } = null!;
    public DbSet<PageComponent> PageComponents { get; set; } = null!;
    public DbSet<Album> Albums { get; set; } = null!;
    public DbSet<AlbumFace> AlbumFaces { get; set; } = null!;
    public DbSet<AlbumComment> AlbumComments { get; set; } = null!;
    public DbSet<AlbumLike> AlbumLikes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Face entity
        builder.Entity<Face>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Index).IsUnique();
            entity.Property(e => e.Index).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.GradientSettings).HasColumnType("text");
            entity.Property(e => e.CreatedAt).IsRequired();

            // One-to-many relationship: Face -> Pages
            entity.HasMany(e => e.Pages)
                  .WithOne(p => p.Face)
                  .HasForeignKey(p => p.FaceId)
                  .OnDelete(DeleteBehavior.Cascade); // If Face is deleted, delete all Pages
        });

        // Configure Page entity
        builder.Entity<Page>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Index).IsRequired();
            entity.Property(e => e.GridSchema).HasColumnType("text");
            entity.Property(e => e.CreatedAt).IsRequired();

            // Foreign key relationship to Face
            entity.HasIndex(e => e.FaceId);

            // Many-to-one relationship: Page -> PageType
            entity.HasOne(e => e.PageType)
                  .WithMany(pt => pt.Pages)
                  .HasForeignKey(e => e.PageTypeId)
                  .OnDelete(DeleteBehavior.Restrict); // Prevent deletion if pages exist
        });

        // Configure PageType entity
        builder.Entity<PageType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Index).IsUnique();
            entity.Property(e => e.Index).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();

            // One-to-many relationship: PageType -> Pages
            entity.HasMany(e => e.Pages)
                  .WithOne(p => p.PageType)
                  .HasForeignKey(p => p.PageTypeId)
                  .OnDelete(DeleteBehavior.Restrict); // Prevent deletion if pages exist
        });

        // Configure PageRouteTranslation entity
        builder.Entity<PageRouteTranslation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LanguageCode).IsRequired().HasMaxLength(10);
            entity.Property(e => e.TranslatedRoute).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).IsRequired();

            // Many-to-one relationship: PageRouteTranslation -> Page
            entity.HasOne(e => e.Page)
                  .WithMany(p => p.RouteTranslations)
                  .HasForeignKey(e => e.PageId)
                  .OnDelete(DeleteBehavior.Cascade); // If Page is deleted, delete all translations

            // Unique constraint: one translation per language per page
            entity.HasIndex(e => new { e.PageId, e.LanguageCode }).IsUnique();
        });

        // Configure UserProfile entity
        builder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450); // Identity user ID length
            entity.Property(e => e.Nickname).HasMaxLength(100);
            entity.Property(e => e.Rod).HasMaxLength(10); // "M", "F", "O", etc.
            entity.Property(e => e.CreatedAt).IsRequired();

            // One-to-one relationship: ApplicationUser -> UserProfile
            entity.HasOne(e => e.User)
                  .WithOne(u => u.UserProfile)
                  .HasForeignKey<UserProfile>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade); // If User is deleted, delete UserProfile

            // Ensure one UserProfile per User
            entity.HasIndex(e => e.UserId).IsUnique();

            // One-to-many relationship: UserProfile -> UserFaceProfiles
            entity.HasMany(e => e.UserFaceProfiles)
                  .WithOne(ufp => ufp.UserProfile)
                  .HasForeignKey(ufp => ufp.UserProfileId)
                  .OnDelete(DeleteBehavior.Cascade); // If UserProfile is deleted, delete all UserFaceProfiles
        });

        // Configure UserFaceProfile entity
        builder.Entity<UserFaceProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserProfileId).IsRequired();
            entity.Property(e => e.FaceId).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.Settings).HasColumnType("text"); // JSON string
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).IsRequired();

            // Many-to-one relationship: UserFaceProfile -> UserProfile
            entity.HasOne(e => e.UserProfile)
                  .WithMany(up => up.UserFaceProfiles)
                  .HasForeignKey(e => e.UserProfileId)
                  .OnDelete(DeleteBehavior.Cascade); // If UserProfile is deleted, delete UserFaceProfile

            // Many-to-one relationship: UserFaceProfile -> Face
            entity.HasOne(e => e.Face)
                  .WithMany(f => f.UserFaceProfiles)
                  .HasForeignKey(e => e.FaceId)
                  .OnDelete(DeleteBehavior.Cascade); // If Face is deleted, delete all UserFaceProfiles

            // Ensure unique combination: one UserFaceProfile per UserProfile+Face pair
            entity.HasIndex(e => new { e.UserProfileId, e.FaceId }).IsUnique();

            // Index on FaceId for faster queries
            entity.HasIndex(e => e.FaceId);
        });

        // Configure UserRole entity
        builder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Scope).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasMany(e => e.Users)
                  .WithOne(u => u.UserRole)
                  .HasForeignKey(u => u.UserRoleId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.UserFaceRoles)
                  .WithOne(ufr => ufr.UserRole)
                  .HasForeignKey(ufr => ufr.UserRoleId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure UserFaceRole - one face role per user per face
        builder.Entity<UserFaceRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.FaceId });
            entity.HasIndex(e => new { e.UserId, e.FaceId }).IsUnique();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Face)
                  .WithMany()
                  .HasForeignKey(e => e.FaceId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.UserRole)
                  .WithMany(r => r.UserFaceRoles)
                  .HasForeignKey(e => e.UserRoleId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure FriendRequest entity
        builder.Entity<FriendRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SenderId, e.ReceiverId }).IsUnique();
            entity.Property(e => e.SenderId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.ReceiverId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Receiver)
                .WithMany()
                .HasForeignKey(e => e.ReceiverId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Friendship entity
        builder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.FriendId }).IsUnique();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.FriendId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Friend)
                .WithMany()
                .HasForeignKey(e => e.FriendId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Message entity
        builder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SenderId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.ReceiverId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.SentAt).IsRequired();
            entity.HasIndex(e => new { e.SenderId, e.ReceiverId });
            entity.HasIndex(e => new { e.ReceiverId, e.SenderId });

            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Receiver)
                .WithMany()
                .HasForeignKey(e => e.ReceiverId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Notification entity
        builder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure UserBlock entity
        builder.Entity<UserBlock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BlockerId, e.BlockedId }).IsUnique();
            entity.Property(e => e.BlockerId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.BlockedId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Blocker)
                .WithMany()
                .HasForeignKey(e => e.BlockerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Blocked)
                .WithMany()
                .HasForeignKey(e => e.BlockedId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure UserFollow entity
        builder.Entity<UserFollow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FollowerId, e.FollowedId }).IsUnique();
            entity.Property(e => e.FollowerId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.FollowedId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Follower)
                .WithMany()
                .HasForeignKey(e => e.FollowerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Followed)
                .WithMany()
                .HasForeignKey(e => e.FollowedId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ApplicationUser entity - UserRole relationship
        builder.Entity<ApplicationUser>(entity =>
        {
            // Many-to-one relationship: ApplicationUser -> UserRole
            entity.HasOne(e => e.UserRole)
                  .WithMany(r => r.Users)
                  .HasForeignKey(e => e.UserRoleId)
                  .OnDelete(DeleteBehavior.Restrict); // Prevent deletion if users exist

            // Ensure UserRoleId is required
            entity.Property(e => e.UserRoleId).IsRequired();
        });

        // Configure ComponentType entity (lookup table with fixed IDs)
        builder.Entity<ComponentType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Index).IsUnique();
            entity.Property(e => e.Index).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        // Configure DisplayMode entity (lookup table with fixed IDs)
        builder.Entity<DisplayMode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Index).IsUnique();
            entity.Property(e => e.Index).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        // Configure PageComponent entity
        builder.Entity<PageComponent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PageId, e.GridKey }).IsUnique();
            entity.Property(e => e.GridKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Label).HasMaxLength(200);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Icon).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Page)
                .WithMany(p => p.Components)
                .HasForeignKey(e => e.PageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ComponentType)
                .WithMany()
                .HasForeignKey(e => e.ComponentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.DisplayMode)
                .WithMany()
                .HasForeignKey(e => e.DisplayModeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Album entity
        builder.Entity<Album>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatorId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.AlbumType).IsRequired();
            entity.Property(e => e.MediaType).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.CreatorId);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AlbumFace entity (many-to-many: Album <-> Face)
        builder.Entity<AlbumFace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AlbumId, e.FaceId }).IsUnique();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Album)
                .WithMany(a => a.AlbumFaces)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Face)
                .WithMany()
                .HasForeignKey(e => e.FaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure AlbumComment entity
        builder.Entity<AlbumComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.AlbumId);

            entity.HasOne(e => e.Album)
                .WithMany(a => a.Comments)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AlbumLike entity
        builder.Entity<AlbumLike>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AlbumId, e.UserId }).IsUnique();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Album)
                .WithMany(a => a.Likes)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
