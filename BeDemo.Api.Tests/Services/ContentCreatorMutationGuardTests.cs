using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests.Services;

/// <summary>BE-RA6…RA12 — creator edit/delete guard parity with <see cref="ContentModerationHelpers"/>.</summary>
public sealed class ContentCreatorMutationGuardTests
{
    [Theory]
    [InlineData(ContentApprovalStatus.PendingApproval)]
    [InlineData(ContentApprovalStatus.Rejected)]
    public void BE_RA6_TryConflictIfNotEditable_AllowsPendingAndRejected(ContentApprovalStatus status)
    {
        ContentCreatorMutationGuard.TryConflictIfNotEditable(status, ContentCreatorMutationGuard.AlbumsContentKind)
            .Should().BeNull();
    }

    [Theory]
    [InlineData(ContentApprovalStatus.Approved)]
    [InlineData(ContentApprovalStatus.Removed)]
    public void BE_RA7_TryConflictIfNotEditable_BlocksPublishedStates(ContentApprovalStatus status)
    {
        var result = ContentCreatorMutationGuard.TryConflictIfNotEditable(
            status,
            ContentCreatorMutationGuard.BlogsContentKind);
        result.Should().BeOfType<ConflictObjectResult>();
        var conflict = (ConflictObjectResult)result!;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        conflict.Value.Should().BeEquivalentTo(new
        {
            error = "Only pending or rejected blogs can be edited by the creator",
        });
    }

    [Theory]
    [InlineData(ContentApprovalStatus.PendingApproval)]
    [InlineData(ContentApprovalStatus.Rejected)]
    public void BE_RA8_TryConflictIfNotDeletable_AllowsPendingAndRejected(ContentApprovalStatus status)
    {
        ContentCreatorMutationGuard.TryConflictIfNotDeletable(status, ContentCreatorMutationGuard.ReelsContentKind)
            .Should().BeNull();
    }

    [Theory]
    [InlineData(ContentApprovalStatus.Approved)]
    [InlineData(ContentApprovalStatus.Removed)]
    public void BE_RA9_TryConflictIfNotDeletable_BlocksPublishedStates(ContentApprovalStatus status)
    {
        var result = ContentCreatorMutationGuard.TryConflictIfNotDeletable(
            status,
            ContentCreatorMutationGuard.ReelsContentKind);
        result.Should().BeOfType<ConflictObjectResult>();
        ((ConflictObjectResult)result!).Value.Should().BeEquivalentTo(new
        {
            error = "Only pending or rejected reels can be deleted by the creator",
        });
    }

    [Fact]
    public void BE_RA10_EditConflictMessage_UsesContentKindPlural()
    {
        var result = ContentCreatorMutationGuard.TryConflictIfNotEditable(
            ContentApprovalStatus.Approved,
            ContentCreatorMutationGuard.AlbumsContentKind);
        ((ConflictObjectResult)result!).Value.Should().BeEquivalentTo(new
        {
            error = "Only pending or rejected albums can be edited by the creator",
        });
    }

    [Fact]
    public void BE_RA11_DeleteConflictMessage_UsesContentKindPlural()
    {
        var result = ContentCreatorMutationGuard.TryConflictIfNotDeletable(
            ContentApprovalStatus.Approved,
            ContentCreatorMutationGuard.BlogsContentKind);
        ((ConflictObjectResult)result!).Value.Should().BeEquivalentTo(new
        {
            error = "Only pending or rejected blogs can be deleted by the creator",
        });
    }

    [Fact]
    public void BE_RA12_Guard_AlignsWithContentModerationHelpers()
    {
        foreach (ContentApprovalStatus status in Enum.GetValues<ContentApprovalStatus>())
        {
            var editable = ContentModerationHelpers.IsCreatorEditable(status);
            (ContentCreatorMutationGuard.TryConflictIfNotEditable(status, "items") == null)
                .Should().Be(editable);

            var deletable = ContentModerationHelpers.IsCreatorDeletable(status);
            (ContentCreatorMutationGuard.TryConflictIfNotDeletable(status, "items") == null)
                .Should().Be(deletable);
        }
    }
}
