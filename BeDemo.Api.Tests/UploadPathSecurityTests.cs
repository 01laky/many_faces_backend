using BeDemo.Api.Utils;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>SHV2 BE-U4 — path traversal guards for wwwroot upload writes.</summary>
[Trait("Category", "BackendSecurity")]
public sealed class UploadPathSecurityTests
{
    [Theory]
    [InlineData("../etc")]
    [InlineData("..")]
    [InlineData("user/id")]
    [InlineData("user\\id")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSafePathSegment_rejects_unsafe_values(string segment)
    {
        UploadPathSecurity.IsSafePathSegment(segment).Should().BeFalse();
    }

    [Theory]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    [InlineData("42")]
    [InlineData("global.png")]
    public void IsSafePathSegment_accepts_normal_segments(string segment)
    {
        UploadPathSecurity.IsSafePathSegment(segment).Should().BeTrue();
    }

    [Fact]
    public void TryResolveFileUnderWebRoot_rejects_traversal_in_user_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "mf-upload-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var ok = UploadPathSecurity.TryResolveFileUnderWebRoot(
                root,
                ["uploads", "avatars", "..", "secret"],
                "face_1.png",
                out _,
                out var error);

            ok.Should().BeFalse();
            error.Should().Be("Invalid upload path");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolveFileUnderWebRoot_resolves_file_inside_web_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "mf-upload-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var ok = UploadPathSecurity.TryResolveFileUnderWebRoot(
                root,
                ["uploads", "avatars", "user-abc"],
                "global.jpg",
                out var fullPath,
                out var error);

            ok.Should().BeTrue();
            error.Should().BeNull();
            fullPath.Should().StartWith(Path.GetFullPath(root));
            Path.GetDirectoryName(fullPath).Should().Contain("user-abc");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FormatMaxFileSizeMessage_matches_avatar_limit()
    {
        UploadLimits.FormatMaxFileSizeMessage(UploadLimits.AvatarMaxBytes)
            .Should().Be("File too large. Max 30 MB.");
    }
}
