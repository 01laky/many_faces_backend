# Backend reference — database ER diagram

**Navigation:** [« Index](../DETAILED_README.md) · [Part 1](./01-features-running-and-api.md) · [Part 2](./02-routing-config-and-workflow.md) · [Part 3](./03-testing-integration-and-troubleshooting.md) · **Part 4 (this file)**

---

## Database Schema Diagram

The database schema diagram is automatically generated after each migration and displayed below:

<!-- AUTO-GENERATED DATABASE DIAGRAM - DO NOT EDIT -->

```mermaid
erDiagram

    AiReviewJobs {
        integer Id PK NOT NULL
        integer ContentType NOT NULL
        integer ContentId NOT NULL
        integer FaceId NOT NULL
        varchar CreatedByUserId NOT NULL
        integer Priority NOT NULL
        integer Status NOT NULL
        integer Attempts NOT NULL
        integer MaxAttempts NOT NULL
        integer ModerationVersion NOT NULL
        timestamp NextAttemptAtUtc
        timestamp CreatedAtUtc NOT NULL
        timestamp StartedAtUtc
        timestamp CompletedAtUtc
        varchar LastError
    }

    AlbumComments {
        integer Id PK NOT NULL
        integer AlbumId NOT NULL
        varchar UserId NOT NULL
        varchar Content NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    AlbumFaces {
        integer Id PK NOT NULL
        integer AlbumId NOT NULL
        integer FaceId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    AlbumLikes {
        integer Id PK NOT NULL
        integer AlbumId NOT NULL
        varchar UserId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    Albums {
        integer Id PK NOT NULL
        varchar CreatorId NOT NULL
        varchar Title NOT NULL
        varchar Description
        integer AlbumType NOT NULL
        integer MediaType NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
        double AiReviewConfidence
        integer AiReviewDecision NOT NULL
        text AiReviewFlagsJson
        varchar AiReviewModelVersion
        varchar AiReviewReason
        integer AiReviewRiskLevel NOT NULL
        integer AiReviewStatus NOT NULL
        varchar AiReviewTraceId
        varchar AiReviewUserMessage
        timestamp AiReviewedAtUtc
        integer ApprovalStatus NOT NULL
        varchar HumanDecisionReason
        timestamp HumanReviewedAtUtc
        varchar HumanReviewedByUserId
        integer ModerationVersion NOT NULL
        varchar RemovalReason
        timestamp RemovedAtUtc
        varchar RemovedByUserId
        timestamp SubmittedAtUtc
    }

    AspNetRoleClaims {
        integer Id PK NOT NULL
        text RoleId NOT NULL
        text ClaimType
        text ClaimValue
    }

    AspNetRoles {
        text Id PK NOT NULL
        varchar Name
        varchar NormalizedName
        text ConcurrencyStamp
    }

    AspNetUserClaims {
        integer Id PK NOT NULL
        text UserId NOT NULL
        text ClaimType
        text ClaimValue
    }

    AspNetUserLogins {
        text LoginProvider PK NOT NULL
        text ProviderKey PK NOT NULL
        text ProviderDisplayName
        text UserId NOT NULL
    }

    AspNetUserRoles {
        text UserId PK NOT NULL
        text UserId NOT NULL
        text RoleId PK NOT NULL
        text RoleId NOT NULL
    }

    AspNetUserTokens {
        text UserId PK NOT NULL
        text UserId NOT NULL
        text LoginProvider PK NOT NULL
        text Name PK NOT NULL
        text Value
    }

    AspNetUsers {
        text Id PK NOT NULL
        text FirstName
        text LastName
        timestamp CreatedAt NOT NULL
        varchar UserName
        varchar NormalizedUserName
        varchar Email
        varchar NormalizedEmail
        boolean EmailConfirmed NOT NULL
        text PasswordHash
        text SecurityStamp
        text ConcurrencyStamp
        text PhoneNumber
        boolean PhoneNumberConfirmed NOT NULL
        boolean TwoFactorEnabled NOT NULL
        timestamp LockoutEnd
        boolean LockoutEnabled NOT NULL
        integer AccessFailedCount NOT NULL
        integer UserRoleId NOT NULL
        integer AccessTokenVersion NOT NULL
    }

    BlogComments {
        integer Id PK NOT NULL
        integer BlogId NOT NULL
        varchar UserId NOT NULL
        varchar Content NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    BlogImages {
        integer Id PK NOT NULL
        integer BlogId NOT NULL
        varchar ImageUrl NOT NULL
        integer SortOrder NOT NULL
        timestamp CreatedAt NOT NULL
    }

    BlogLikes {
        integer Id PK NOT NULL
        integer BlogId NOT NULL
        varchar UserId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    Blogs {
        integer Id PK NOT NULL
        varchar CreatorId NOT NULL
        integer FaceId NOT NULL
        varchar Title NOT NULL
        text Content NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
        double AiReviewConfidence
        integer AiReviewDecision NOT NULL
        text AiReviewFlagsJson
        varchar AiReviewModelVersion
        varchar AiReviewReason
        integer AiReviewRiskLevel NOT NULL
        integer AiReviewStatus NOT NULL
        varchar AiReviewTraceId
        varchar AiReviewUserMessage
        timestamp AiReviewedAtUtc
        integer ApprovalStatus NOT NULL
        varchar HumanDecisionReason
        timestamp HumanReviewedAtUtc
        varchar HumanReviewedByUserId
        integer ModerationVersion NOT NULL
        varchar RemovalReason
        timestamp RemovedAtUtc
        varchar RemovedByUserId
        timestamp SubmittedAtUtc
    }

    ComponentTypes {
        integer Id PK NOT NULL
        varchar Index NOT NULL
        varchar Name NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    ContentModerationEvents {
        integer Id PK NOT NULL
        integer ContentType NOT NULL
        integer ContentId NOT NULL
        integer FaceId NOT NULL
        integer OldApprovalStatus
        integer NewApprovalStatus
        integer OldAiReviewStatus
        integer NewAiReviewStatus
        integer ActorType NOT NULL
        varchar ActorUserId
        varchar Reason
        varchar UserMessage
        varchar AiTraceId
        varchar AiModelVersion
        timestamp CreatedAtUtc NOT NULL
    }

    DisplayModes {
        integer Id PK NOT NULL
        varchar Index NOT NULL
        varchar Name NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    FaceChatRoomJoinRequests {
        integer Id PK NOT NULL
        integer FaceChatRoomId NOT NULL
        varchar UserId NOT NULL
        integer Status NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp ResolvedAt
    }

    FaceChatRoomMembers {
        integer Id PK NOT NULL
        integer FaceChatRoomId NOT NULL
        varchar UserId NOT NULL
        timestamp JoinedAt NOT NULL
    }

    FaceChatRoomMessages {
        integer Id PK NOT NULL
        integer FaceChatRoomId NOT NULL
        varchar SenderUserId NOT NULL
        varchar Content NOT NULL
        timestamp SentAt NOT NULL
    }

    FaceChatRooms {
        integer Id PK NOT NULL
        integer FaceId NOT NULL
        varchar Title NOT NULL
        varchar Description
        boolean IsPublic NOT NULL
        boolean IsSystemManaged NOT NULL
        text CreatorUserId
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
        timestamp LastMessageAt
    }

    FaceWallTicketComments {
        integer Id PK NOT NULL
        integer FaceWallTicketId NOT NULL
        varchar UserId NOT NULL
        varchar Content NOT NULL
        timestamp CreatedAt NOT NULL
    }

    FaceWallTicketLikes {
        integer Id PK NOT NULL
        integer FaceWallTicketId NOT NULL
        varchar UserId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    FaceWallTickets {
        integer Id PK NOT NULL
        integer FaceId NOT NULL
        varchar CreatorUserId NOT NULL
        varchar Title NOT NULL
        text Description NOT NULL
        integer Status NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    Faces {
        integer Id PK NOT NULL
        varchar Index NOT NULL
        varchar Title NOT NULL
        varchar Description
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
        boolean IsPublic NOT NULL
        text GradientSettings
        boolean AllowRecensions NOT NULL
        integer Visibility NOT NULL
        boolean ChatRoomsCreate NOT NULL
    }

    FriendRequests {
        integer Id PK NOT NULL
        varchar SenderId NOT NULL
        varchar ReceiverId NOT NULL
        integer Status NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp RespondedAt
    }

    Friendships {
        integer Id PK NOT NULL
        varchar UserId NOT NULL
        varchar FriendId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    Messages {
        integer Id PK NOT NULL
        varchar SenderId NOT NULL
        varchar ReceiverId NOT NULL
        text Content NOT NULL
        timestamp SentAt NOT NULL
        timestamp ReadAt
        boolean IsMessageRequest NOT NULL
        integer MessageRequestStatus
    }

    Notifications {
        integer Id PK NOT NULL
        varchar UserId NOT NULL
        varchar Title NOT NULL
        text Message NOT NULL
        varchar Type NOT NULL
        timestamp CreatedAt NOT NULL
    }

    OAuthClients {
        integer Id PK NOT NULL
        varchar ClientId NOT NULL
        varchar SecretHash NOT NULL
        boolean IsActive NOT NULL
        timestamp CreatedAtUtc NOT NULL
    }

    OAuthRefreshTokens {
        integer Id PK NOT NULL
        varchar TokenHash NOT NULL
        varchar UserId NOT NULL
        timestamp ExpiresAtUtc NOT NULL
        timestamp CreatedAtUtc NOT NULL
        boolean UseRememberMeAccessLifetime NOT NULL
        timestamp RevokedAtUtc
        varchar ReplacedByTokenHash
    }

    PageComponents {
        integer Id PK NOT NULL
        integer PageId NOT NULL
        integer ComponentTypeId NOT NULL
        integer DisplayModeId NOT NULL
        varchar GridKey NOT NULL
        integer X NOT NULL
        integer Y NOT NULL
        integer W NOT NULL
        integer H NOT NULL
        integer MinW NOT NULL
        integer MinH NOT NULL
        varchar Label
        varchar Title
        varchar Icon
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    PageRouteTranslations {
        integer Id PK NOT NULL
        integer PageId NOT NULL
        varchar LanguageCode NOT NULL
        varchar TranslatedRoute NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    PageTypes {
        integer Id PK NOT NULL
        varchar Index NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    Pages {
        integer Id PK NOT NULL
        integer FaceId NOT NULL
        integer PageTypeId NOT NULL
        varchar Name NOT NULL
        varchar Description
        varchar Path NOT NULL
        integer Index NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
        text GridSchema
    }

    ReelComments {
        integer Id PK NOT NULL
        integer ReelId NOT NULL
        varchar UserId NOT NULL
        varchar Content NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    ReelFaces {
        integer Id PK NOT NULL
        integer ReelId NOT NULL
        integer FaceId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    ReelLikes {
        integer Id PK NOT NULL
        integer ReelId NOT NULL
        varchar UserId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    Reels {
        integer Id PK NOT NULL
        varchar CreatorId NOT NULL
        varchar Title NOT NULL
        varchar Description
        varchar VideoUrl NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
        double AiReviewConfidence
        integer AiReviewDecision NOT NULL
        text AiReviewFlagsJson
        varchar AiReviewModelVersion
        varchar AiReviewReason
        integer AiReviewRiskLevel NOT NULL
        integer AiReviewStatus NOT NULL
        varchar AiReviewTraceId
        varchar AiReviewUserMessage
        timestamp AiReviewedAtUtc
        integer ApprovalStatus NOT NULL
        varchar HumanDecisionReason
        timestamp HumanReviewedAtUtc
        varchar HumanReviewedByUserId
        integer ModerationVersion NOT NULL
        varchar RemovalReason
        timestamp RemovedAtUtc
        varchar RemovedByUserId
        timestamp SubmittedAtUtc
    }

    Stories {
        integer Id PK NOT NULL
        varchar CreatorId NOT NULL
        varchar Title NOT NULL
        integer State NOT NULL
        timestamp PublishedAt
        timestamp ExpiresAt
        timestamp ScheduledPublishAt
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    StoryComments {
        integer Id PK NOT NULL
        integer StoryId NOT NULL
        varchar UserId NOT NULL
        varchar Content NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
    }

    StoryFaces {
        integer Id PK NOT NULL
        integer StoryId NOT NULL
        integer FaceId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    StoryImages {
        integer Id PK NOT NULL
        integer StoryId NOT NULL
        varchar ImageUrl NOT NULL
        varchar Description
        integer SortOrder NOT NULL
        timestamp CreatedAt NOT NULL
    }

    StoryLikes {
        integer Id PK NOT NULL
        integer StoryId NOT NULL
        varchar UserId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    StoryViews {
        integer Id PK NOT NULL
        integer StoryId NOT NULL
        varchar ViewerUserId NOT NULL
        timestamp ViewedAt NOT NULL
    }

    UserBlocks {
        integer Id PK NOT NULL
        varchar BlockerId NOT NULL
        varchar BlockedId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    UserFaceProfileComments {
        integer Id PK NOT NULL
        integer UserFaceProfileId NOT NULL
        varchar UserId NOT NULL
        varchar Body NOT NULL
        timestamp CreatedAt NOT NULL
    }

    UserFaceProfileLikes {
        integer Id PK NOT NULL
        integer UserFaceProfileId NOT NULL
        varchar UserId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    UserFaceProfileReviews {
        integer Id PK NOT NULL
        integer UserFaceProfileId NOT NULL
        varchar AuthorUserId NOT NULL
        varchar Title NOT NULL
        varchar Text NOT NULL
        smallint Stars NOT NULL
        timestamp CreatedAt NOT NULL
    }

    UserFaceProfiles {
        integer Id PK NOT NULL
        integer UserProfileId NOT NULL
        integer FaceId NOT NULL
        varchar DisplayName
        varchar AvatarUrl
        text Settings
        boolean IsActive NOT NULL
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
        boolean FaceRoleIntroCompleted NOT NULL
        boolean Visited NOT NULL
    }

    UserFaceRoles {
        varchar UserId PK NOT NULL
        varchar UserId NOT NULL
        integer FaceId NOT NULL
        integer FaceId PK NOT NULL
        integer UserRoleId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    UserFollows {
        integer Id PK NOT NULL
        varchar FollowerId NOT NULL
        varchar FollowedId NOT NULL
        timestamp CreatedAt NOT NULL
    }

    UserProfiles {
        integer Id PK NOT NULL
        varchar UserId NOT NULL
        varchar Nickname
        integer Age
        varchar Rod
        timestamp CreatedAt NOT NULL
        timestamp UpdatedAt
        text AvatarUrl
    }

    UserRoles {
        integer Id PK NOT NULL
        varchar Name NOT NULL
        varchar Description
        timestamp CreatedAt NOT NULL
        integer Scope NOT NULL
    }

    Albums ||--o{ AlbumComments : "has"
    AspNetUsers ||--o{ AlbumComments : "has"
    Albums ||--o{ AlbumFaces : "has"
    Faces ||--o{ AlbumFaces : "has"
    Albums ||--o{ AlbumLikes : "has"
    AspNetUsers ||--o{ AlbumLikes : "has"
    AspNetUsers ||--o{ Albums : "has"
    AspNetRoles ||--o{ AspNetRoleClaims : "has"
    AspNetUsers ||--o{ AspNetUserClaims : "has"
    AspNetUsers ||--o{ AspNetUserLogins : "has"
    AspNetRoles ||--o{ AspNetUserRoles : "has"
    AspNetUsers ||--o{ AspNetUserRoles : "has"
    AspNetUsers ||--o{ AspNetUserTokens : "has"
    UserRoles ||--o{ AspNetUsers : "has"
    AspNetUsers ||--o{ BlogComments : "has"
    Blogs ||--o{ BlogComments : "has"
    Blogs ||--o{ BlogImages : "has"
    AspNetUsers ||--o{ BlogLikes : "has"
    Blogs ||--o{ BlogLikes : "has"
    AspNetUsers ||--o{ Blogs : "has"
    Faces ||--o{ Blogs : "has"
    AspNetUsers ||--o{ FaceChatRoomJoinRequests : "has"
    FaceChatRooms ||--o{ FaceChatRoomJoinRequests : "has"
    AspNetUsers ||--o{ FaceChatRoomMembers : "has"
    FaceChatRooms ||--o{ FaceChatRoomMembers : "has"
    AspNetUsers ||--o{ FaceChatRoomMessages : "has"
    FaceChatRooms ||--o{ FaceChatRoomMessages : "has"
    AspNetUsers ||--o{ FaceChatRooms : "has"
    Faces ||--o{ FaceChatRooms : "has"
    AspNetUsers ||--o{ FaceWallTicketComments : "has"
    FaceWallTickets ||--o{ FaceWallTicketComments : "has"
    AspNetUsers ||--o{ FaceWallTicketLikes : "has"
    FaceWallTickets ||--o{ FaceWallTicketLikes : "has"
    AspNetUsers ||--o{ FaceWallTickets : "has"
    Faces ||--o{ FaceWallTickets : "has"
    AspNetUsers ||--o{ FriendRequests : "has"
    AspNetUsers ||--o{ FriendRequests : "has"
    AspNetUsers ||--o{ Friendships : "has"
    AspNetUsers ||--o{ Friendships : "has"
    AspNetUsers ||--o{ Messages : "has"
    AspNetUsers ||--o{ Messages : "has"
    AspNetUsers ||--o{ Notifications : "has"
    AspNetUsers ||--o{ OAuthRefreshTokens : "has"
    ComponentTypes ||--o{ PageComponents : "has"
    DisplayModes ||--o{ PageComponents : "has"
    Pages ||--o{ PageComponents : "has"
    Pages ||--o{ PageRouteTranslations : "has"
    Faces ||--o{ Pages : "has"
    PageTypes ||--o{ Pages : "has"
    AspNetUsers ||--o{ ReelComments : "has"
    Reels ||--o{ ReelComments : "has"
    Faces ||--o{ ReelFaces : "has"
    Reels ||--o{ ReelFaces : "has"
    AspNetUsers ||--o{ ReelLikes : "has"
    Reels ||--o{ ReelLikes : "has"
    AspNetUsers ||--o{ Reels : "has"
    AspNetUsers ||--o{ Stories : "has"
    AspNetUsers ||--o{ StoryComments : "has"
    Stories ||--o{ StoryComments : "has"
    Faces ||--o{ StoryFaces : "has"
    Stories ||--o{ StoryFaces : "has"
    Stories ||--o{ StoryImages : "has"
    AspNetUsers ||--o{ StoryLikes : "has"
    Stories ||--o{ StoryLikes : "has"
    AspNetUsers ||--o{ StoryViews : "has"
    Stories ||--o{ StoryViews : "has"
    AspNetUsers ||--o{ UserBlocks : "has"
    AspNetUsers ||--o{ UserBlocks : "has"
    AspNetUsers ||--o{ UserFaceProfileComments : "has"
    UserFaceProfiles ||--o{ UserFaceProfileComments : "has"
    AspNetUsers ||--o{ UserFaceProfileLikes : "has"
    UserFaceProfiles ||--o{ UserFaceProfileLikes : "has"
    AspNetUsers ||--o{ UserFaceProfileReviews : "has"
    UserFaceProfiles ||--o{ UserFaceProfileReviews : "has"
    Faces ||--o{ UserFaceProfiles : "has"
    UserProfiles ||--o{ UserFaceProfiles : "has"
    Faces ||--o{ UserFaceRoles : "has"
    UserRoles ||--o{ UserFaceRoles : "has"
    AspNetUsers ||--o{ UserFaceRoles : "has"
    AspNetUsers ||--o{ UserFollows : "has"
    AspNetUsers ||--o{ UserFollows : "has"
    AspNetUsers ||--o{ UserProfiles : "has"
```


<!-- END AUTO-GENERATED DATABASE DIAGRAM -->
