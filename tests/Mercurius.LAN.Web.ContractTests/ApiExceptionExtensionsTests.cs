using System.Net;
using System.Net.Http;
using System.Text;
using Mercurius.LAN.Web.Extensions;
using Refit;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class ApiExceptionExtensionsTests
{
    [Fact]
    public async Task GetApiError_ParsesCodeAndMessageFromTheSharedErrorShape()
    {
        var exception = await CreateApiException(
            "{\"code\":\"match_reversal_blocked\",\"message\":\"A downstream result exists.\"}");

        var error = exception.GetApiError();

        Assert.NotNull(error);
        Assert.Equal("match_reversal_blocked", error.Code);
        Assert.Equal("A downstream result exists.", error.Message);
    }

    [Theory]
    [InlineData("\"A plain error message.\"", "A plain error message.")]
    [InlineData("A legacy plain-text error.", "A legacy plain-text error.")]
    public async Task GetApiError_PreservesStringAndPlainTextBodies(string content, string expectedMessage)
    {
        var exception = await CreateApiException(content);

        var error = exception.GetApiError();

        Assert.NotNull(error);
        Assert.Null(error.Code);
        Assert.Equal(expectedMessage, error.Message);
    }

    [Theory]
    [InlineData("""{"type":"about:blank","title":"One or more validation errors occurred.","status":400,"detail":"A leaderboard tournament requires at least one valid recorded result before completion."}""")]
    [InlineData("""{"status":400,"detail":"A leaderboard tournament requires at least one valid recorded result before completion."}""")]
    public async Task GetApiError_ReadsProblemDetailsDetail(string content)
    {
        var exception = await CreateApiException(content);

        var error = exception.GetApiError();

        Assert.NotNull(error);
        Assert.Null(error.Code);
        Assert.Equal(
            "A leaderboard tournament requires at least one valid recorded result before completion.",
            error.Message);
    }

    [Fact]
    public async Task GetApiError_ReadsFirstValidationProblemError()
    {
        var exception = await CreateApiException(
            """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"state":["A supported tournament lifecycle state is required."]}}""");

        var error = exception.GetApiError();

        Assert.NotNull(error);
        Assert.Null(error.Code);
        Assert.Equal("A supported tournament lifecycle state is required.", error.Message);
    }

    [Fact]
    public async Task GetApiError_FallsBackToTitleForADetailFreeProblemBody()
    {
        var exception = await CreateApiException(
            """{"type":"about:blank","title":"One or more validation errors occurred.","status":400}""");

        var error = exception.GetApiError();

        Assert.NotNull(error);
        Assert.Null(error.Code);
        Assert.Equal("One or more validation errors occurred.", error.Message);
    }

    [Fact]
    public async Task GetApiError_ReadsTheRefitValidationExceptionFromProblemJson()
    {
        var exception = await CreateApiException(
            """{"type":"about:blank","title":"One or more validation errors occurred.","status":409,"detail":"The team changed while you were working.","code":"team_changed"}""",
            "application/problem+json");

        Assert.IsType<ValidationApiException>(exception);

        var error = exception.GetApiError();

        Assert.NotNull(error);
        Assert.Equal("team_changed", error.Code);
        Assert.Equal("The team changed while you were working.", error.Message);
    }

    [Fact]
    public async Task GetApiError_ReadsFirstValidationErrorFromProblemJson()
    {
        var exception = await CreateApiException(
            """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Name":["The name is already in use."]}}""",
            "application/problem+json");

        var error = exception.GetApiError();

        Assert.NotNull(error);
        Assert.Null(error.Code);
        Assert.Equal("The name is already in use.", error.Message);
    }

    [Fact]
    public async Task GetApiError_FallsBackToTitleForDetailFreeProblemJson()
    {
        var exception = await CreateApiException(
            """{"type":"about:blank","title":"One or more validation errors occurred.","status":400}""",
            "application/problem+json");

        var error = exception.GetApiError();

        Assert.NotNull(error);
        Assert.Equal("One or more validation errors occurred.", error.Message);
    }

    [Fact]
    public async Task GetApiError_ReportsNoMessageForAProblemJsonBodyWithoutUserFacingCopy()
    {
        var exception = await CreateApiException(
            """{"status":409,"traceId":"00-abc"}""",
            "application/problem+json");

        var error = exception.GetApiError();

        Assert.NotNull(error);
        Assert.Null(error.Code);
        Assert.True(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task GetApiError_PreservesAPlainTextBodyWhenTheContentTypeIsPlainText()
    {
        var exception = await CreateApiException("The team changed while you were working.", "text/plain");

        var error = exception.GetApiError();

        Assert.NotNull(error);
        Assert.Null(error.Code);
        Assert.Equal("The team changed while you were working.", error.Message);
    }

    private static async Task<ApiException> CreateApiException(string content, string mediaType = "application/json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/matches");
        using var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(content, Encoding.UTF8, mediaType)
        };

        return await ApiException.Create(
            request,
            HttpMethod.Post,
            response,
            new RefitSettings(),
            innerException: null);
    }
}
