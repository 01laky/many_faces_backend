using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Models;

namespace BeDemo.Api.Data;

public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
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
    public DbSet<AlbumMedia> AlbumMedia { get; set; } = null!;
    public DbSet<Blog> Blogs { get; set; } = null!;
    public DbSet<BlogImage> BlogImages { get; set; } = null!;
    public DbSet<BlogComment> BlogComments { get; set; } = null!;
    public DbSet<BlogLike> BlogLikes { get; set; } = null!;
    public DbSet<Reel> Reels { get; set; } = null!;
    public DbSet<ReelFace> ReelFaces { get; set; } = null!;
    public DbSet<ReelComment> ReelComments { get; set; } = null!;
    public DbSet<ReelLike> ReelLikes { get; set; } = null!;
    public DbSet<Story> Stories { get; set; } = null!;
    public DbSet<StoryFace> StoryFaces { get; set; } = null!;
    public DbSet<StoryImage> StoryImages { get; set; } = null!;
    public DbSet<StoryLike> StoryLikes { get; set; } = null!;
    public DbSet<StoryComment> StoryComments { get; set; } = null!;
    public DbSet<StoryView> StoryViews { get; set; } = null!;
    public DbSet<FaceChatRoom> FaceChatRooms { get; set; } = null!;
    public DbSet<FaceChatRoomMember> FaceChatRoomMembers { get; set; } = null!;
    public DbSet<FaceChatRoomMessage> FaceChatRoomMessages { get; set; } = null!;
    public DbSet<FaceChatRoomJoinRequest> FaceChatRoomJoinRequests { get; set; } = null!;
    public DbSet<FaceVideoLounge> FaceVideoLounges { get; set; } = null!;
    public DbSet<FaceVideoLoungeMember> FaceVideoLoungeMembers { get; set; } = null!;
    public DbSet<FaceVideoLoungeJoinRequest> FaceVideoLoungeJoinRequests { get; set; } = null!;
    public DbSet<FaceVideoLoungeSession> FaceVideoLoungeSessions { get; set; } = null!;
    public DbSet<FaceVideoLoungeSessionParticipant> FaceVideoLoungeSessionParticipants { get; set; } = null!;
    public DbSet<FaceWallTicket> FaceWallTickets { get; set; } = null!;
    public DbSet<FaceWallTicketComment> FaceWallTicketComments { get; set; } = null!;
    public DbSet<FaceWallTicketLike> FaceWallTicketLikes { get; set; } = null!;
    public DbSet<UserFaceProfileLike> UserFaceProfileLikes { get; set; } = null!;
    public DbSet<UserFaceProfileComment> UserFaceProfileComments { get; set; } = null!;
    public DbSet<UserFaceProfileReview> UserFaceProfileReviews { get; set; } = null!;
    public DbSet<AiReviewJob> AiReviewJobs { get; set; } = null!;
    public DbSet<ContentModerationEvent> ContentModerationEvents { get; set; } = null!;

    /// <summary>OAuth2 refresh token persistence (rotation, revocation) — see <see cref="IOAuthRefreshTokenStore"/>.</summary>
    public DbSet<OAuthRefreshToken> OAuthRefreshTokens { get; set; } = null!;

    /// <summary>OAuth2 confidential clients with hashed secrets (O1).</summary>
    public DbSet<OAuthClient> OAuthClients { get; set; } = null!;

    /// <summary>FCM / push registration rows for signed-in mobile clients (<c>POST /api/me/push-token</c>).</summary>
    public DbSet<UserPushDevice> UserPushDevices { get; set; } = null!;

    /// <summary>Pending email-code signups (<c>POST /api/oauth2/register/request</c>).</summary>
    public DbSet<RegistrationInvite> RegistrationInvites { get; set; } = null!;

    /// <summary>Shared operator AI support threads (admin chat).</summary>
    public DbSet<OperatorAiConversation> OperatorAiConversations { get; set; } = null!;

    /// <summary>Messages within <see cref="OperatorAiConversation"/>.</summary>
    public DbSet<OperatorAiMessage> OperatorAiMessages { get; set; } = null!;

    /// <summary>Latest AI worker host hardware snapshot (GetHostProfile).</summary>
    public DbSet<AiWorkerHostProfile> AiWorkerHostProfiles { get; set; } = null!;

    /// <summary>Global Redis TTL for operator AI live stats bundle cache (singleton Id=1).</summary>
    public DbSet<OperatorAiLiveStatsCacheSettings> OperatorAiLiveStatsCacheSettings { get; set; } = null!;
    public DbSet<OperatorAiPublicStatsSettings> OperatorAiPublicStatsSettings { get; set; } = null!;
    public DbSet<OperatorAiSystemSettings> OperatorAiSystemSettings { get; set; } = null!;

    /// <summary>Singleton refresh metadata for AI worker host profile.</summary>
    public DbSet<AiWorkerHostRefreshMeta> AiWorkerHostRefreshMetas { get; set; } = null!;

    /// <summary>Operator face-scoped bans (not peer <see cref="UserBlock"/>).</summary>
    public DbSet<UserFaceModeration> UserFaceModerations { get; set; } = null!;

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
            entity.Property(e => e.GradientSettings).HasColumnType("text");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Visibility).IsRequired().HasConversion<int>();
            entity.Property(e => e.AllowRecensions).IsRequired();
            entity.Property(e => e.ChatRoomsCreate).IsRequired();
            entity.Property(e => e.VideoLoungesCreate).IsRequired();

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
            entity.Property(e => e.EnableAnimatedGradient).HasDefaultValue(false);
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
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.Visited).IsRequired();
            entity.Property(e => e.FaceRoleIntroCompleted).IsRequired();
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
            entity.Property(e => e.ApprovalStatus).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewStatus).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewDecision).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewRiskLevel).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewFlagsJson).HasColumnType("text");
            entity.Property(e => e.AiReviewReason).HasMaxLength(2000);
            entity.Property(e => e.AiReviewUserMessage).HasMaxLength(1000);
            entity.Property(e => e.AiReviewModelVersion).HasMaxLength(100);
            entity.Property(e => e.AiReviewTraceId).HasMaxLength(200);
            entity.Property(e => e.HumanReviewedByUserId).HasMaxLength(450);
            entity.Property(e => e.HumanDecisionReason).HasMaxLength(2000);
            entity.Property(e => e.RemovedByUserId).HasMaxLength(450);
            entity.Property(e => e.RemovalReason).HasMaxLength(2000);
            entity.Property(e => e.ModerationVersion).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.CreatorId);
            entity.HasIndex(e => e.ApprovalStatus);
            entity.HasIndex(e => e.AiReviewStatus);

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

        builder.Entity<AlbumMedia>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AlbumId);
            entity.Property(e => e.ImageUrl).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.VideoUrl).HasMaxLength(2048);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(2048);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.MediaType).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Album)
                .WithMany(a => a.MediaItems)
                .HasForeignKey(e => e.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Blog entity
        builder.Entity<Blog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatorId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Content).IsRequired().HasColumnType("text");
            entity.Property(e => e.ApprovalStatus).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewStatus).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewDecision).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewRiskLevel).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewFlagsJson).HasColumnType("text");
            entity.Property(e => e.AiReviewReason).HasMaxLength(2000);
            entity.Property(e => e.AiReviewUserMessage).HasMaxLength(1000);
            entity.Property(e => e.AiReviewModelVersion).HasMaxLength(100);
            entity.Property(e => e.AiReviewTraceId).HasMaxLength(200);
            entity.Property(e => e.HumanReviewedByUserId).HasMaxLength(450);
            entity.Property(e => e.HumanDecisionReason).HasMaxLength(2000);
            entity.Property(e => e.RemovedByUserId).HasMaxLength(450);
            entity.Property(e => e.RemovalReason).HasMaxLength(2000);
            entity.Property(e => e.ModerationVersion).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.CreatorId);
            entity.HasIndex(e => e.FaceId);
            entity.HasIndex(e => e.ApprovalStatus);
            entity.HasIndex(e => e.AiReviewStatus);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Face)
                .WithMany()
                .HasForeignKey(e => e.FaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure BlogImage entity (max 3 per blog, enforced at controller level)
        builder.Entity<BlogImage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ImageUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Blog)
                .WithMany(b => b.Images)
                .HasForeignKey(e => e.BlogId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BlogComment entity
        builder.Entity<BlogComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.BlogId);

            entity.HasOne(e => e.Blog)
                .WithMany(b => b.Comments)
                .HasForeignKey(e => e.BlogId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BlogLike entity
        builder.Entity<BlogLike>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BlogId, e.UserId }).IsUnique();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Blog)
                .WithMany(b => b.Likes)
                .HasForeignKey(e => e.BlogId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Reel entity
        builder.Entity<Reel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatorId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.VideoUrl).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ApprovalStatus).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewStatus).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewDecision).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewRiskLevel).IsRequired().HasConversion<int>();
            entity.Property(e => e.AiReviewFlagsJson).HasColumnType("text");
            entity.Property(e => e.AiReviewReason).HasMaxLength(2000);
            entity.Property(e => e.AiReviewUserMessage).HasMaxLength(1000);
            entity.Property(e => e.AiReviewModelVersion).HasMaxLength(100);
            entity.Property(e => e.AiReviewTraceId).HasMaxLength(200);
            entity.Property(e => e.HumanReviewedByUserId).HasMaxLength(450);
            entity.Property(e => e.HumanDecisionReason).HasMaxLength(2000);
            entity.Property(e => e.RemovedByUserId).HasMaxLength(450);
            entity.Property(e => e.RemovalReason).HasMaxLength(2000);
            entity.Property(e => e.ModerationVersion).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.CreatorId);
            entity.HasIndex(e => e.ApprovalStatus);
            entity.HasIndex(e => e.AiReviewStatus);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReelFace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ReelId, e.FaceId }).IsUnique();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Reel)
                .WithMany(r => r.ReelFaces)
                .HasForeignKey(e => e.ReelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Face)
                .WithMany()
                .HasForeignKey(e => e.FaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReelComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.ReelId);

            entity.HasOne(e => e.Reel)
                .WithMany(r => r.Comments)
                .HasForeignKey(e => e.ReelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReelLike>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ReelId, e.UserId }).IsUnique();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Reel)
                .WithMany(r => r.Likes)
                .HasForeignKey(e => e.ReelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Story>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatorId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.State).IsRequired().HasConversion<int>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.CreatorId);
            entity.HasIndex(e => new { e.State, e.PublishedAt, e.ExpiresAt });

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StoryFace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StoryId, e.FaceId }).IsUnique();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Story)
                .WithMany(s => s.StoryFaces)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Face)
                .WithMany()
                .HasForeignKey(e => e.FaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StoryImage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ImageUrl).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => new { e.StoryId, e.SortOrder }).IsUnique();

            entity.HasOne(e => e.Story)
                .WithMany(s => s.Images)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StoryLike>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StoryId, e.UserId }).IsUnique();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.Story)
                .WithMany(s => s.Likes)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StoryComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.StoryId);

            entity.HasOne(e => e.Story)
                .WithMany(s => s.Comments)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StoryView>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StoryId, e.ViewerUserId }).IsUnique();
            entity.Property(e => e.ViewerUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.ViewedAt).IsRequired();

            entity.HasOne(e => e.Story)
                .WithMany(s => s.Views)
                .HasForeignKey(e => e.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Viewer)
                .WithMany()
                .HasForeignKey(e => e.ViewerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserFaceProfileLike>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserFaceProfileId, e.UserId }).IsUnique();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.UserFaceProfile)
                .WithMany(p => p.ProfileLikes)
                .HasForeignKey(e => e.UserFaceProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserFaceProfileComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Body).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.UserFaceProfileId);

            entity.HasOne(e => e.UserFaceProfile)
                .WithMany(p => p.ProfileComments)
                .HasForeignKey(e => e.UserFaceProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserFaceProfileReview>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserFaceProfileId, e.AuthorUserId }).IsUnique();
            entity.Property(e => e.AuthorUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Text).IsRequired().HasMaxLength(8000);
            entity.Property(e => e.Stars).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(e => e.UserFaceProfile)
                .WithMany(p => p.ProfileReviews)
                .HasForeignKey(e => e.UserFaceProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Author)
                .WithMany()
                .HasForeignKey(e => e.AuthorUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FaceChatRoom>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasIndex(e => e.FaceId);

            entity.HasOne(e => e.Face)
                .WithMany(f => f.ChatRooms)
                .HasForeignKey(e => e.FaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<FaceChatRoomMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FaceChatRoomId, e.UserId }).IsUnique();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);

            entity.HasOne(e => e.Room)
                .WithMany(r => r.Members)
                .HasForeignKey(e => e.FaceChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FaceChatRoomMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(8000);
            entity.Property(e => e.SenderUserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => e.FaceChatRoomId);
            entity.HasIndex(e => e.SentAt);

            entity.HasOne(e => e.Room)
                .WithMany(r => r.Messages)
                .HasForeignKey(e => e.FaceChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FaceChatRoomJoinRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Status).IsRequired().HasConversion<int>();
            entity.HasIndex(e => new { e.FaceChatRoomId, e.UserId, e.Status });

            entity.HasOne(e => e.Room)
                .WithMany(r => r.JoinRequests)
                .HasForeignKey(e => e.FaceChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FaceVideoLounge>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasIndex(e => e.FaceId);

            entity.HasOne(e => e.Face)
                .WithMany(f => f.VideoLounges)
                .HasForeignKey(e => e.FaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<FaceVideoLoungeMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FaceVideoLoungeId, e.UserId }).IsUnique();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);

            entity.HasOne(e => e.Lounge)
                .WithMany(r => r.Members)
                .HasForeignKey(e => e.FaceVideoLoungeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FaceVideoLoungeJoinRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Status).IsRequired().HasConversion<int>();
            entity.HasIndex(e => new { e.FaceVideoLoungeId, e.UserId, e.Status });

            entity.HasOne(e => e.Lounge)
                .WithMany(r => r.JoinRequests)
                .HasForeignKey(e => e.FaceVideoLoungeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FaceVideoLoungeSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StartedByUserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => e.FaceVideoLoungeId);
            entity.HasIndex(e => new { e.FaceVideoLoungeId, e.EndedAt });

            entity.HasOne(e => e.Lounge)
                .WithMany(r => r.Sessions)
                .HasForeignKey(e => e.FaceVideoLoungeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FaceVideoLoungeSessionParticipant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.JoinMode).IsRequired().HasConversion<int>();
            entity.HasIndex(e => new { e.FaceVideoLoungeSessionId, e.UserId, e.LeftAt });

            entity.HasOne(e => e.Session)
                .WithMany(s => s.Participants)
                .HasForeignKey(e => e.FaceVideoLoungeSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FaceWallTicket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasColumnType("text");
            entity.Property(e => e.CreatorUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Status).IsRequired().HasConversion<int>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.FaceId);
            entity.HasIndex(e => new { e.FaceId, e.CreatorUserId });

            entity.HasOne(e => e.Face)
                .WithMany()
                .HasForeignKey(e => e.FaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FaceWallTicketComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.FaceWallTicketId);

            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Comments)
                .HasForeignKey(e => e.FaceWallTicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FaceWallTicketLike>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => new { e.FaceWallTicketId, e.UserId }).IsUnique();

            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Likes)
                .HasForeignKey(e => e.FaceWallTicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OAuthRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.ReplacedByTokenHash).HasMaxLength(64);
            entity.HasIndex(e => e.TokenHash);
            entity.HasIndex(e => e.UserId);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OAuthClient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.SecretHash).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.ClientId).IsUnique();
        });

        builder.Entity<AiReviewJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ContentType).IsRequired().HasConversion<int>();
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Status).IsRequired().HasConversion<int>();
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => new { e.ContentType, e.ContentId, e.ModerationVersion }).IsUnique();
            entity.HasIndex(e => new { e.Status, e.NextAttemptAtUtc });
            entity.HasIndex(e => e.FaceId);
        });

        builder.Entity<ContentModerationEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ContentType).IsRequired().HasConversion<int>();
            entity.Property(e => e.OldApprovalStatus).HasConversion<int>();
            entity.Property(e => e.NewApprovalStatus).HasConversion<int>();
            entity.Property(e => e.OldAiReviewStatus).HasConversion<int>();
            entity.Property(e => e.NewAiReviewStatus).HasConversion<int>();
            entity.Property(e => e.ActorType).IsRequired().HasConversion<int>();
            entity.Property(e => e.ActorUserId).HasMaxLength(450);
            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.UserMessage).HasMaxLength(1000);
            entity.Property(e => e.AiTraceId).HasMaxLength(200);
            entity.Property(e => e.AiModelVersion).HasMaxLength(100);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => new { e.ContentType, e.ContentId, e.CreatedAtUtc });
            entity.HasIndex(e => e.FaceId);
        });

        // FCM device tokens: one registration token globally unique; optional per-user installation upsert when InstallationId is set.
        builder.Entity<UserPushDevice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(32);
            entity.Property(e => e.RegistrationToken).IsRequired().HasMaxLength(512);
            entity.Property(e => e.InstallationId).HasMaxLength(200);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.HasIndex(e => e.RegistrationToken).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.InstallationId })
                .IsUnique()
                .HasFilter("\"InstallationId\" IS NOT NULL");
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RegistrationInvite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.NormalizedEmail).IsRequired().HasMaxLength(256);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.LinkHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.CodeHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Locale).IsRequired().HasMaxLength(16);
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
            entity.HasIndex(e => e.LinkHash).IsUnique();
            entity.HasIndex(e => new { e.NormalizedEmail })
                .IsUnique()
                .HasFilter("\"ConsumedAtUtc\" IS NULL AND \"RevokedAtUtc\" IS NULL");
            entity.HasIndex(e => e.ExpiresAtUtc);
        });

        builder.Entity<OperatorAiConversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasIndex(e => e.UpdatedAt);
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OperatorAiMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.StatsMode).HasMaxLength(16);
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
            entity.Property(e => e.AuthorEmail).HasMaxLength(256);
            entity.Property(e => e.ResponseLocale).HasMaxLength(8);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => new { e.ConversationId, e.Id });
            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AiWorkerHostProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkerInstanceId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.GrpcAddressLastSeen).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ProfileJson).IsRequired();
            entity.Property(e => e.Hostname).HasMaxLength(256);
            entity.Property(e => e.OsDisplayName).HasMaxLength(256);
            entity.Property(e => e.GpuPrimaryName).HasMaxLength(256);
            entity.Property(e => e.CollectedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.HasIndex(e => e.WorkerInstanceId).IsUnique();
            entity.HasIndex(e => e.UpdatedAtUtc);
        });

        builder.Entity<OperatorAiLiveStatsCacheSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TtlMilliseconds).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedByUserId).HasMaxLength(450);
        });

        builder.Entity<OperatorAiPublicStatsSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PublicStatsMode).IsRequired().HasMaxLength(16);
            entity.Property(e => e.LiveMaxParallelBundleCalls).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedByUserId).HasMaxLength(450);
        });

        builder.Entity<OperatorAiSystemSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AiEnabled).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedByUserId).HasMaxLength(450);
            entity.Property(e => e.LastEnableHealthStatus).HasMaxLength(64);
        });

        builder.Entity<AiWorkerHostRefreshMeta>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LastRefreshError).HasMaxLength(2000);
            entity.Property(e => e.GrpcAddressConfigured).HasMaxLength(512);
        });

        builder.Entity<UserFaceModeration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.BannedAt).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.FaceId, e.LiftedAt });
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Face)
                .WithMany()
                .HasForeignKey(e => e.FaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.BannedByUser)
                .WithMany()
                .HasForeignKey(e => e.BannedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
