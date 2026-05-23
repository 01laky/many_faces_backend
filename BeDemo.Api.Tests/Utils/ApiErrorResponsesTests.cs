using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Tests.Utils;

/// <summary>BE-RA1…RA5 — legacy <c>{ error }</c> JSON helpers.</summary>
public sealed class ApiErrorResponsesTests
{
    [Fact]
    public void BE_RA1_Error_WrapsMessageInAnonymousObject()
    {
        var payload = ApiErrorResponses.Error("Something failed");
        payload.Should().BeEquivalentTo(new { error = "Something failed" });
    }

    [Fact]
    public void BE_RA2_NotFound_Returns404WithErrorShape()
    {
        var result = ApiErrorResponses.NotFound("Face not found");
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        result.Value.Should().BeEquivalentTo(new { error = "Face not found" });
    }

    [Fact]
    public void BE_RA3_BadRequest_Returns400WithErrorShape()
    {
        var result = ApiErrorResponses.BadRequest("Invalid input");
        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Value.Should().BeEquivalentTo(new { error = "Invalid input" });
    }

    [Fact]
    public void BE_RA4_Conflict_Returns409WithErrorShape()
    {
        var result = ApiErrorResponses.Conflict("Only pending or rejected albums can be edited by the creator");
        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        result.Value.Should().BeEquivalentTo(new { error = "Only pending or rejected albums can be edited by the creator" });
    }

    [Fact]
    public void BE_RA5_ForbiddenAndInternalServerError_SetExplicitStatusCodes()
    {
        var forbidden = ApiErrorResponses.Forbidden("Face not accessible");
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        forbidden.Value.Should().BeEquivalentTo(new { error = "Face not accessible" });

        var serverError = ApiErrorResponses.InternalServerError("An error occurred while setting face role");
        serverError.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        serverError.Value.Should().BeEquivalentTo(new { error = "An error occurred while setting face role" });
    }
}
