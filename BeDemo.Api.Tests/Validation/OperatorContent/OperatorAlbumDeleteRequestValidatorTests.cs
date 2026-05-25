using BeDemo.Api.Models.Requests.OperatorContent;
using BeDemo.Api.Validation.OperatorContent;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.OperatorContent;

public sealed class OperatorAlbumDeleteRequestValidatorTests
{
	private readonly OperatorAlbumDeleteRequestValidator _sut = new();

	private static OperatorAlbumDeleteRequest Valid() => new()
	{
		FaceId = 1,
		Reason = "Audit reason long enough",
		UserMessage = "Creator message long enough",
	};

	[Fact]
	public void Valid_minimal_payload_has_no_errors()
	{
		_sut.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Missing_reason_fails()
	{
		var model = Valid();
		model.Reason = "";
		_sut.TestValidate(model).ShouldHaveValidationErrorFor(x => x.Reason);
	}

	[Fact]
	public void Reason_length_9_fails()
	{
		var model = Valid();
		model.Reason = "123456789";
		_sut.TestValidate(model).ShouldHaveValidationErrorFor(x => x.Reason);
	}

	[Fact]
	public void Missing_user_message_fails()
	{
		var model = Valid();
		model.UserMessage = "";
		_sut.TestValidate(model).ShouldHaveValidationErrorFor(x => x.UserMessage);
	}

	[Fact]
	public void User_message_length_9_fails()
	{
		var model = Valid();
		model.UserMessage = "123456789";
		_sut.TestValidate(model).ShouldHaveValidationErrorFor(x => x.UserMessage);
	}

	[Fact]
	public void Face_id_zero_fails()
	{
		var model = Valid();
		model.FaceId = 0;
		_sut.TestValidate(model).ShouldHaveValidationErrorFor(x => x.FaceId);
	}
}
