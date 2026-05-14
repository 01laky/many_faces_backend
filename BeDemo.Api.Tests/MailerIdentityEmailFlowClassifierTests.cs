using BeDemo.Api.Services;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Ensures Identity HTML fragments map to the same template ids the Java worker allow-lists.
/// </summary>
public sealed class MailerIdentityEmailFlowClassifierTests
{
    [Fact]
    public void ClassifyTemplateId_detects_reset_from_path_marker()
    {
        var html = """<a href="https://app.test/Account/ResetPassword?code=x">link</a>""";
        MailerIdentityEmailFlowClassifier.ClassifyTemplateId(html)
            .Should()
            .Be(MailerIdentityEmailFlowClassifier.TemplateIdentityPasswordReset);
    }

    [Fact]
    public void ClassifyTemplateId_detects_confirm_from_keyword()
    {
        var html = """Please <a href="https://app.test/x">ConfirmEmail</a> now""";
        MailerIdentityEmailFlowClassifier.ClassifyTemplateId(html)
            .Should()
            .Be(MailerIdentityEmailFlowClassifier.TemplateIdentityEmailConfirm);
    }

    [Fact]
    public void ExtractCallbackUrl_grabs_first_http_href()
    {
        var html = """<p><a href="https://host/Account/ConfirmEmail?u=1&c=abc">x</a></p>""";
        MailerIdentityEmailFlowClassifier.ExtractCallbackUrl(html)
            .Should()
            .Be("https://host/Account/ConfirmEmail?u=1&c=abc");
    }
}
