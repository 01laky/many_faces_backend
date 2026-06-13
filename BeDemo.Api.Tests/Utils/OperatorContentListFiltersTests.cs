using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Utils;

/// <summary>
/// Edge-case coverage for <see cref="OperatorContentListFilters.ApplyAlbumPortalVisibility"/>, the portal album
/// visibility predicate. Pins the operator-sees-all bypass and the non-operator rule (approved AND
/// (public OR own)), including the unauthenticated case (empty userId → only public approved), which is the
/// behaviour the <c>userId ?? string.Empty</c> fix in AlbumGridListService relies on.
/// </summary>
public sealed class OperatorContentListFiltersTests
{
	private static Album Album(int id, AlbumTypeEnum type, ContentApprovalStatus status, string creatorId) =>
		new() { Id = id, AlbumType = type, ApprovalStatus = status, CreatorId = creatorId };

	private static readonly Album[] Corpus =
	{
		Album(1, AlbumTypeEnum.Public, ContentApprovalStatus.Approved, "userA"), // public approved
		Album(2, AlbumTypeEnum.Private, ContentApprovalStatus.Approved, "userA"), // userA's private
		Album(3, AlbumTypeEnum.Private, ContentApprovalStatus.Approved, "userB"), // userB's private
		Album(4, AlbumTypeEnum.Public, ContentApprovalStatus.PendingApproval, "userA"), // not approved
		Album(5, AlbumTypeEnum.Public, ContentApprovalStatus.Rejected, "userB"), // not approved
	};

	private static int[] VisibleIds(bool operatorInventory, string userId) =>
		OperatorContentListFilters
			.ApplyAlbumPortalVisibility(Corpus.AsQueryable(), operatorInventory, userId)
			.Select(a => a.Id)
			.OrderBy(id => id)
			.ToArray();

	[Fact]
	public void Operator_inventory_sees_every_album_unfiltered()
	{
		VisibleIds(operatorInventory: true, userId: "anyone").Should().Equal(1, 2, 3, 4, 5);
	}

	[Fact]
	public void Non_operator_sees_public_approved_plus_own_approved_private()
	{
		VisibleIds(operatorInventory: false, userId: "userA").Should().Equal(1, 2);
		VisibleIds(operatorInventory: false, userId: "userB").Should().Equal(1, 3);
	}

	[Fact]
	public void Unauthenticated_empty_userId_sees_only_public_approved()
	{
		// The AlbumGridListService fix passes string.Empty for a null userId; an empty id matches no real
		// CreatorId, so an anonymous viewer never sees a private album — only public approved ones.
		VisibleIds(operatorInventory: false, userId: string.Empty).Should().Equal(1);
	}

	[Fact]
	public void Non_operator_never_sees_unapproved_content_even_when_public_or_own()
	{
		// Album 4 (public, pending, userA) and album 5 (public, rejected) must be hidden from non-operators.
		VisibleIds(operatorInventory: false, userId: "userA").Should().NotContain(new[] { 4, 5 });
		VisibleIds(operatorInventory: false, userId: "userB").Should().NotContain(new[] { 4, 5 });
	}
}
