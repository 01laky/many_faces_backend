using System.Text;
using BeDemo.Api.Services.OperatorPush;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Services;

/// <summary>
/// Characterization tests for <see cref="FirebaseServiceAccountValidator.TryValidate"/> (backend-refactor §4.5, 0
/// tests): the untrusted-JSON validator for uploaded FCM service-account credentials. Covers the happy path plus
/// every rejection branch (nullness, size cap, malformed JSON, wrong type, and each required field).
/// </summary>
public sealed class FirebaseServiceAccountValidatorTests
{
	private const string Valid =
		"{\"type\":\"service_account\",\"project_id\":\"my-proj\"," +
		"\"private_key\":\"-----BEGIN PRIVATE KEY-----\\nabc\\n-----END PRIVATE KEY-----\\n\"," +
		"\"client_email\":\"svc@my-proj.iam.gserviceaccount.com\"}";

	[Fact]
	public void Valid_service_account_returns_true_and_trimmed_project_id()
	{
		FirebaseServiceAccountValidator.TryValidate(Valid, out var projectId, out var error).Should().BeTrue();
		projectId.Should().Be("my-proj");
		error.Should().BeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Null_or_blank_is_rejected(string? json)
	{
		FirebaseServiceAccountValidator.TryValidate(json!, out var projectId, out var error).Should().BeFalse();
		projectId.Should().BeNull();
		error.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void Oversize_json_is_rejected()
	{
		var huge = "{\"type\":\"service_account\",\"pad\":\"" + new string('x', 33 * 1024) + "\"}";
		FirebaseServiceAccountValidator.TryValidate(huge, out _, out var error).Should().BeFalse();
		error.Should().NotBeNullOrEmpty();
	}

	[Theory]
	[InlineData("not json at all")]
	[InlineData("{ broken")]
	public void Malformed_json_is_rejected(string json)
	{
		FirebaseServiceAccountValidator.TryValidate(json, out _, out var error).Should().BeFalse();
		error.Should().NotBeNullOrEmpty();
	}

	[Theory]
	[InlineData("{\"type\":\"user\",\"project_id\":\"p\",\"private_key\":\"BEGIN x\",\"client_email\":\"e@x\"}")]                 // wrong type
	[InlineData("{\"type\":\"service_account\",\"private_key\":\"BEGIN x\",\"client_email\":\"e@x\"}")]                           // no project_id
	[InlineData("{\"type\":\"service_account\",\"project_id\":\"p\",\"private_key\":\"no-marker\",\"client_email\":\"e@x\"}")]    // private_key without BEGIN
	[InlineData("{\"type\":\"service_account\",\"project_id\":\"p\",\"private_key\":\"BEGIN x\"}")]                               // no client_email
	public void Missing_or_invalid_required_fields_are_rejected(string json)
	{
		FirebaseServiceAccountValidator.TryValidate(json, out var projectId, out var error).Should().BeFalse();
		projectId.Should().BeNull();
		error.Should().NotBeNullOrEmpty();
	}
}
