using System.ComponentModel.DataAnnotations;
using BeDemo.Api.Models.Requests.OperatorUsers;
using FluentAssertions;

namespace BeDemo.Api.Tests;

public class OperatorBanReasonValidatorTests
{
    private static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void BanReason_ShouldFail_WhenMissingOrTooShort()
    {
        Validate(new OperatorBanReasonRequest()).Should().NotBeEmpty();
        Validate(new OperatorBanReasonRequest { Reason = "" }).Should().NotBeEmpty();
        Validate(new OperatorBanReasonRequest { Reason = "short" }).Should().NotBeEmpty();
    }

    [Fact]
    public void BanReason_ShouldPass_WhenTrimmedLengthAtLeastTen()
    {
        Validate(new OperatorBanReasonRequest { Reason = "1234567890" }).Should().BeEmpty();
        Validate(new OperatorBanReasonRequest { Reason = "  valid reason text  " }).Should().BeEmpty();
    }

    [Fact]
    public void BanReason_ShouldFail_WhenExceedsMaxLength()
    {
        Validate(new OperatorBanReasonRequest { Reason = new string('x', 2001) }).Should().NotBeEmpty();
    }

    [Fact]
    public void PlatformMessage_ShouldRequireContent()
    {
        Validate(new OperatorPlatformMessageRequest()).Should().NotBeEmpty();
        Validate(new OperatorPlatformMessageRequest { Content = "hello" }).Should().BeEmpty();
    }
}
