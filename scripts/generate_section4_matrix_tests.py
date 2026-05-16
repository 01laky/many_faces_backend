#!/usr/bin/env python3
"""Generate ValidatorSection4MatrixTests.cs — §4 T1 + T11 for every FluentValidation schema."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VAL = ROOT / "BeDemo.Api" / "Validation"
OUT = ROOT / "BeDemo.Api.Tests" / "Validation" / "ValidatorSection4MatrixTests.cs"

# Validators that need DI / manual construction — covered in dedicated *ValidatorTests.cs
SKIP_VALIDATORS = frozenset({
    "RegisterCompleteDtoValidator",
    "OAuth2TokenRequestValidator",
    "DeletePushTokenQueryValidator",
    "LocalizationBundleQueryValidator",
    "ModerationDecisionRequestValidator",
})

VALIDATION_USINGS = sorted(
    f"BeDemo.Api.Validation.{p.name}"
    for p in (ROOT / "BeDemo.Api" / "Validation").iterdir()
    if p.is_dir() and not p.name.startswith(".")
)

EXTRA_USINGS = [
    "BeDemo.Api.Models",
    "BeDemo.Api.Models.DTOs",
    "BeDemo.Api.Models.Requests.Albums",
    "BeDemo.Api.Models.Requests.Auth",
    "BeDemo.Api.Models.Requests.Blogs",
    "BeDemo.Api.Models.Requests.Common",
    "BeDemo.Api.Models.Requests.Faces",
    "BeDemo.Api.Models.Requests.Moderation",
    "BeDemo.Api.Models.Requests.OAuth",
    "BeDemo.Api.Models.Requests.Pages",
    "BeDemo.Api.Models.Requests.Profile",
    "BeDemo.Api.Models.Requests.Reels",
    "BeDemo.Api.Models.Requests.Social",
    "BeDemo.Api.Models.Requests.Stats",
    "BeDemo.Api.Models.Requests.Stories",
    "BeDemo.Api.Models.Requests.Users",
]

SAMPLES: dict[str, tuple[str, str]] = {
    "LoginModel": ("new LoginModel()", "new LoginModel { Email = \"a@b.com\", Password = \"secret\" }"),
    "RegisterModel": ("new RegisterModel()", "new RegisterModel { Email = \"a@b.com\", Password = \"Test1234!@##\" }"),
    "OAuth2TokenRequest": (
        "new OAuth2TokenRequest { GrantType = \"\", ClientId = \"c\", ClientSecret = \"s\" }",
        "new OAuth2TokenRequest { GrantType = \"password\", ClientId = \"be-demo-client\", ClientSecret = \"be-demo-secret-very-strong-key\", Username = \"u@test.com\", Password = \"Test1234!@##\" }",
    ),
    "RegisterRequestDto": ("new RegisterRequestDto()", "new RegisterRequestDto { Email = \"a@b.com\" }"),
    "RegisterResendDto": ("new RegisterResendDto()", "new RegisterResendDto { Email = \"a@b.com\" }"),
    "RegisterCompleteDto": (
        "new RegisterCompleteDto()",
        "new RegisterCompleteDto { Hash = \"h\", Code = \"123456\", Password = \"Test1234!@##\" }",
    ),
    "RegisterPrefillQuery": ("new RegisterPrefillQuery()", "new RegisterPrefillQuery { Hash = \"abc\" }"),
    "GetUsersQuery": ("new GetUsersQuery { Page = 0 }", "new GetUsersQuery { Page = 1, PageSize = 10 }"),
    "CreateUserModel": ("new CreateUserModel()", "new CreateUserModel { Email = \"a@b.com\", Password = \"Test1234!@##\" }"),
    "RegisterPushTokenRequestDto": (
        "new RegisterPushTokenRequestDto()",
        "new RegisterPushTokenRequestDto { RegistrationToken = \"0123456789\", Platform = \"ios\" }",
    ),
    "StoryListQuery": ("new StoryListQuery()", "new StoryListQuery { FaceId = 1 }"),
    "StoryDetailQuery": ("new StoryDetailQuery()", "new StoryDetailQuery { FaceId = 1 }"),
    "StoryViewQuery": ("new StoryViewQuery()", "new StoryViewQuery { FaceId = 1 }"),
    "StoryScopedQuery": ("new StoryScopedQuery()", "new StoryScopedQuery { FaceId = 1 }"),
    "CreateStoryDto": ("new CreateStoryDto()", "new CreateStoryDto { Title = \"T\" }"),
    "CreateReelDto": (
        "new CreateReelDto()",
        "new CreateReelDto { Title = \"T\", VideoUrl = \"https://example.com/v.mp4\" }",
    ),
    "CreateBlogDto": ("new CreateBlogDto()", "new CreateBlogDto { Title = \"T\", Content = \"C\", FaceId = 1 }"),
    "CreateAlbumDto": ("new CreateAlbumDto()", "new CreateAlbumDto { Title = \"T\" }"),
    "WallTicketWriteDto": ("new WallTicketWriteDto()", "new WallTicketWriteDto { Title = \"T\", Description = \"D\" }"),
    "WallTicketCommentDto": ("new WallTicketCommentDto()", "new WallTicketCommentDto { Content = \"c\" }"),
    "BulkModerationRequest": (
        "new BulkModerationRequest(BeDemo.Api.Models.Requests.Moderation.BulkModerationAction.Approve, new List<BulkModerationItemDto>(), null, null)",
        "new BulkModerationRequest(BeDemo.Api.Models.Requests.Moderation.BulkModerationAction.Approve, new List<BulkModerationItemDto> { new(ModeratedContentType.Blog, 1) }, null, null)",
    ),
    "PublishStoryDto": ("new PublishStoryDto { ScheduledPublishAt = DateTime.UtcNow.AddDays(-1) }", "new PublishStoryDto()"),
    "UpsertPageTranslationsRequest": (
        "new UpsertPageTranslationsRequest()",
        "new UpsertPageTranslationsRequest { Translations = [new PageTranslationItem { LanguageCode = \"en\", TranslatedRoute = \"/x\" }] }",
    ),
    "CreatePageTypeModel": ("new CreatePageTypeModel()", "new CreatePageTypeModel { Index = \"home\" }"),
    "UpdatePageTypeModel": ("new UpdatePageTypeModel()", "new UpdatePageTypeModel { Index = \"home\" }"),
    "CreatePageComponentDto": (
        "new CreatePageComponentDto()",
        "new CreatePageComponentDto { PageId = 1, ComponentTypeId = 1, DisplayModeId = 1 }",
    ),
    "UpdatePageComponentDto": ("new UpdatePageComponentDto()", "new UpdatePageComponentDto { Label = \"L\" }"),
    "UpdatePageModel": ("new UpdatePageModel()", "new UpdatePageModel { Name = \"N\" }"),
    "UpdateFaceModel": ("new UpdateFaceModel()", "new UpdateFaceModel { Title = \"T\" }"),
    "UpdateAlbumRequest": ("new UpdateAlbumDto()", "new UpdateAlbumDto { Title = \"T\" }"),
    "UpdateBlogRequest": ("new UpdateBlogDto()", "new UpdateBlogDto { Title = \"T\" }"),
    "UpdateReelRequest": ("new UpdateReelDto()", "new UpdateReelDto { Title = \"T\" }"),
    "GetModerationQueueQuery": ("new GetModerationQueueQuery()", "new GetModerationQueueQuery { FaceId = 1 }"),
    "StatsTimeseriesQuery": (
        "new StatsTimeseriesQuery { Metric = \"\", FromUtc = DateTime.UtcNow, ToUtc = DateTime.UtcNow.AddDays(-1), Bucket = \"day\" }",
        "new StatsTimeseriesQuery { Metric = \"users\", FromUtc = DateTime.UtcNow.AddDays(-7), ToUtc = DateTime.UtcNow, Bucket = \"day\" }",
    ),
    "MessageHistoryQuery": ("new MessageHistoryQuery { Limit = 0 }", "new MessageHistoryQuery { Limit = 50 }"),
    "NotificationsListQuery": ("new NotificationsListQuery { Limit = 0 }", "new NotificationsListQuery { Limit = 50 }"),
    "StoryImageUploadForm": (
        "new StoryImageUploadForm()",
        "new StoryImageUploadForm { File = CreateImageFormFile(), SortOrder = 0 }",
    ),
    "AvatarUploadRequest": (
        "new AvatarUploadRequest()",
        "new AvatarUploadRequest { File = new FormFile(Stream.Null, 0, 1, \"a\", \"a.png\") }",
    ),
    "CreatePageModel": (
        "new CreatePageModel()",
        "new CreatePageModel { FaceId = 1, PageTypeId = 1, Name = \"N\", Path = \"/p\", Index = 0 }",
    ),
    "CreateFaceModel": ("new CreateFaceModel()", "new CreateFaceModel { Index = \"x\", Title = \"T\" }"),
    "SetMyFaceRoleModel": ("new SetMyFaceRoleModel()", "new SetMyFaceRoleModel { UserRoleId = 1 }"),
    "FaceProfileCommentDto": ("new FaceProfileCommentDto()", "new FaceProfileCommentDto { Body = \"b\" }"),
    "FaceProfileReviewDto": (
        "new FaceProfileReviewDto()",
        "new FaceProfileReviewDto { Title = \"T\", Text = \"x\", Stars = 5 }",
    ),
    "BulkModerationRequest": (
        "new BulkModerationRequest(BeDemo.Api.Models.Requests.Moderation.BulkModerationAction.Approve, new List<BulkModerationItemDto>(), null, null)",
        "new BulkModerationRequest(BeDemo.Api.Models.Requests.Moderation.BulkModerationAction.Approve, new List<BulkModerationItemDto> { new(ModeratedContentType.Blog, 1) }, null, null)",
    ),
    "PublishStoryDto": ("new PublishStoryDto { ScheduledPublishAt = DateTime.UtcNow.AddDays(-1) }", "new PublishStoryDto()"),
    "AdminInviteListQuery": ("new AdminInviteListQuery { Take = 0 }", "new AdminInviteListQuery { Skip = 0, Take = 10 }"),
    "ChatMessagesQuery": ("new ChatMessagesQuery { PageSize = 0 }", "new ChatMessagesQuery { PageSize = 20 }"),
    "AlbumListQuery": ("new AlbumListQuery { FaceId = -1 }", "new AlbumListQuery { FaceId = 1 }"),
    "BlogListQuery": ("new BlogListQuery { FaceId = -1 }", "new BlogListQuery { FaceId = 1 }"),
    "ReelListQuery": ("new ReelListQuery { FaceId = -1 }", "new ReelListQuery { FaceId = 1 }"),
    "SendFriendRequestDto": ("new SendFriendRequestDto()", "new SendFriendRequestDto { ReceiverId = \"u\" }"),
    "BlockUserDto": ("new BlockUserDto()", "new BlockUserDto { BlockedId = \"u\" }"),
    "FollowUserDto": ("new FollowUserDto()", "new FollowUserDto { FollowedId = \"u\" }"),
    "ModerationDecisionDto": ("new ModerationDecisionDto(null, null)", "new ModerationDecisionDto(\"r\", null)"),
    "CreatePageTypeModel": ("new CreatePageTypeModel()", "new CreatePageTypeModel { Index = \"home\" }"),
    "UpdatePageTypeModel": ("new UpdatePageTypeModel { Index = new string('x', 100) }", "new UpdatePageTypeModel { Index = \"home\" }"),
    "UpsertPageTranslationsRequest": (
        "new UpsertPageTranslationsRequest()",
        "new UpsertPageTranslationsRequest { Translations = [new PageTranslationItem { LanguageCode = \"en\", TranslatedRoute = \"/x\" }] }",
    ),
    "CreatePageComponentDto": (
        "new CreatePageComponentDto()",
        "new CreatePageComponentDto { PageId = 1, ComponentTypeId = 1, DisplayModeId = 1 }",
    ),
    "UpdatePageComponentDto": ("new UpdatePageComponentDto { Label = new string('x', 500) }", "new UpdatePageComponentDto { Label = \"L\" }"),
    "UpdatePageModel": ("new UpdatePageModel { GridSchema = new string('x', 100_001) }", "new UpdatePageModel { Name = \"N\" }"),
    "UpdateFaceModel": ("new UpdateFaceModel { Title = new string('x', 500) }", "new UpdateFaceModel { Title = \"T\" }"),
    "UpdateAlbumDto": ("new UpdateAlbumDto { Title = new string('x', 500) }", "new UpdateAlbumDto { Title = \"T\" }"),
    "UpdateBlogDto": ("new UpdateBlogDto { Title = new string('x', 500) }", "new UpdateBlogDto { Title = \"T\" }"),
    "UpdateReelDto": ("new UpdateReelDto { Title = new string('x', 500) }", "new UpdateReelDto { Title = \"T\" }"),
    "UpdateUserModel": ("new UpdateUserModel { Password = \"short\" }", "new UpdateUserModel { FirstName = \"A\" }"),
    "UpdateFaceChatRoomDto": ("new UpdateFaceChatRoomDto { Title = new string('x', 500) }", "new UpdateFaceChatRoomDto { Title = \"T\" }"),
    "GetModerationQueueQuery": (
        "new GetModerationQueueQuery { MinConfidence = 0.9, MaxConfidence = 0.1 }",
        "new GetModerationQueueQuery { FaceId = 1 }",
    ),
    "StoryMineQuery": ("new StoryMineQuery { FaceId = -1 }", "new StoryMineQuery()"),
    "ProfileMeQuery": ("new ProfileMeQuery { FaceId = -1 }", "new ProfileMeQuery { FaceId = 1 }"),
    "GetPagesQuery": ("new GetPagesQuery { FaceId = -1 }", "new GetPagesQuery { FaceId = 1 }"),
    "AlbumByUserQuery": ("new AlbumByUserQuery { FaceId = -1 }", "new AlbumByUserQuery { FaceId = 1 }"),
    "ReelDetailQuery": ("new ReelDetailQuery { FaceId = -1 }", "new ReelDetailQuery { FaceId = 1 }"),
    "ReelByUserQuery": ("new ReelByUserQuery { FaceId = -1 }", "new ReelByUserQuery { FaceId = 1 }"),
    "ReelCommentCreateQuery": ("new ReelCommentCreateQuery { FaceId = -1 }", "new ReelCommentCreateQuery { FaceId = 1 }"),
    "FaceAvatarUploadRequest": (
        "new FaceAvatarUploadRequest()",
        "new FaceAvatarUploadRequest { FaceId = 1, File = CreateImageFormFile() }",
    ),
    "UpdateProfileRequest": ("new UpdateProfileRequest()", "new UpdateProfileRequest { FirstName = \"A\" }"),
    "CreateSystemFaceChatRoomDto": ("new CreateSystemFaceChatRoomDto()", "new CreateSystemFaceChatRoomDto { Title = \"T\" }"),
    "CreateFaceChatRoomDto": ("new CreateFaceChatRoomDto()", "new CreateFaceChatRoomDto { Title = \"T\" }"),
    "CreateAlbumCommentDto": ("new CreateAlbumCommentDto { Content = \"\" }", "new CreateAlbumCommentDto { Content = \"x\" }"),
    "UpdateAlbumCommentDto": ("new UpdateAlbumCommentDto { Content = \"\" }", "new UpdateAlbumCommentDto { Content = \"x\" }"),
    "CreateBlogCommentDto": ("new CreateBlogCommentDto { Content = \"\" }", "new CreateBlogCommentDto { Content = \"x\" }"),
    "UpdateBlogCommentDto": ("new UpdateBlogCommentDto { Content = \"\" }", "new UpdateBlogCommentDto { Content = \"x\" }"),
    "CreateReelCommentDto": ("new CreateReelCommentDto { Content = \"\" }", "new CreateReelCommentDto { Content = \"x\" }"),
    "UpdateReelCommentDto": ("new UpdateReelCommentDto { Content = \"\" }", "new UpdateReelCommentDto { Content = \"x\" }"),
}


def infer(short: str) -> tuple[str, str] | None:
    if short in SAMPLES:
        return SAMPLES[short]
    if "Comment" in short and short.endswith("Dto"):
        return (f"new {short} {{ Content = \"\" }}", f"new {short} {{ Content = \"x\" }}")
    if short.startswith("Create") and short.endswith(("Dto", "Model")):
        return (f"new {short}()", f"new {short} {{ Title = \"T\" }}")
    return None


def collect() -> list[tuple[str, str]]:
    out = []
    for path in sorted(VAL.rglob("*Validator.cs")):
        if "Abstract" in path.name or "Filter" in path.name:
            continue
        m = re.search(r"AbstractValidator<([^>]+)>", path.read_text())
        if m:
            if path.stem in SKIP_VALIDATORS:
                continue
            short = m.group(1).strip().split(".")[-1]
            out.append((path.stem, short))
    return out


def main() -> None:
    lines = [
        "// <auto-generated by scripts/generate_section4_matrix_tests.py>",
        *([f"using {u};" for u in EXTRA_USINGS]),
        *([f"using {u};" for u in VALIDATION_USINGS]),
        "using FluentAssertions;",
        "using FluentValidation;",
        "using Microsoft.AspNetCore.Http;",
        "using Xunit;",
        "",
        "namespace BeDemo.Api.Tests.Validation;",
        "",
        "public sealed class ValidatorSection4MatrixTests",
        "{",
        "    private static FormFile CreateImageFormFile()",
        "    {",
        "        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };",
        "        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, \"a\", \"a.png\")",
        "        {",
        "            Headers = new HeaderDictionary(),",
        "            ContentType = \"image/png\",",
        "        };",
        "    }",
        "",
    ]
    skipped: list[str] = []
    for validator, short in collect():
        pair = infer(short)
        if pair is None:
            skipped.append(short)
            continue
        invalid, valid = pair
        lines += [
            f"    [Fact] public void T1_{validator}_invalid_fails() =>",
            f"        new {validator}().Validate({invalid}).IsValid.Should().BeFalse();",
            "",
            f"    [Fact] public void T11_{validator}_valid_minimal_passes() =>",
            f"        new {validator}().Validate({valid}).IsValid.Should().BeTrue();",
            "",
        ]
    if skipped:
        lines.insert(-1, f"    // Skipped matrix (add to SAMPLES in generator): {', '.join(sorted(set(skipped)))}")
    lines.append("}")
    OUT.write_text("\n".join(lines) + "\n")
    print(f"wrote {len(collect()) * 2} tests to {OUT}")


if __name__ == "__main__":
    main()
