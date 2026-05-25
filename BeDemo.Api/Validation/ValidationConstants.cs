namespace BeDemo.Api.Validation;

/// <summary>Shared numeric bounds for FluentValidation (SHV2 endpoint-schema-validation). Prefer EF limits from §18 appendix.</summary>
public static class ValidationConstants
{
	public const int IdentityUserIdMaxLength = 450;
	public const int EmailMaxLength = 256;
	public const int PasswordMaxLength = 128;
	public const int NameMaxLength = 100;
	public const int TitleMaxLength = 200;
	public const int DescriptionShortMaxLength = 1000;
	public const int DescriptionMediumMaxLength = 2000;
	public const int DescriptionLongMaxLength = 4000;
	public const int WallTicketDescriptionMaxLength = 8000;
	public const int WallTicketCommentMaxLength = 255;
	public const int VideoUrlMaxLength = 1000;
	public const int ImageUrlMaxLength = 500;
	public const int RegistrationHashMaxLength = 128;
	public const int OAuthClientFieldMaxLength = 200;
	public const int LocaleMaxLength = 20;
	public const int PagePathMaxLength = 500;
	public const int GridSchemaMaxLength = 100_000;
	public const int BlogContentMaxLength = 100_000;
	public const int ModerationReasonMaxLength = 2000;
	public const int PushTokenMinLength = 10;
	public const int PushTokenMaxLength = 512;
	public const int InstallationIdMaxLength = 200;
	public const int PageSizeDefaultMax = 100;
	public const int MessageLimitMax = 200;
	public const int NotificationLimitMax = 100;
	public const int AvatarMaxBytes = 30 * 1024 * 1024;
	public const int StoryImageMaxBytes = 52_428_800;
	public const int BulkModerationMaxItems = 100;
	public const int MaxFaceIdsPerRequest = 20;
	public const int MaxBlogImages = 3;
	public const int MaxPageTranslations = 50;
	public const int StatsMaxSpanDays = 366;
}
