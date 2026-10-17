using System.Net;
using System.Text;
using System.Text.Json;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Extensions;
using Refit;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class ProfilePictureContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
    private static readonly Guid MockUserId = Guid.Parse("41111111-1111-1111-1111-111111111120");

    [Fact]
    public async Task UploadPicture_CommitsTheMultipartFieldAndProjectsTheNestedPictureUrl()
    {
        var handler = new RecordingHandler(
            "{\"isComplete\":true,\"user\":{\"id\":\"" + MockUserId + "\",\"username\":\"mockuser\",\"profilePictureUrl\":\"images/users/abc.webp\"},\"email\":null,\"emailVerified\":false}");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<IUserClient>(httpClient, CreateRefitSettings());

        using var stream = new MemoryStream([1, 2, 3, 4]);
        var response = await client.UploadCurrentUserProfilePictureAsync(
            new StreamPart(stream, "avatar.png", "image/png"));

        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal("/v1/lan/users/me/picture", handler.Request.RequestUri!.AbsolutePath);
        Assert.Contains("multipart/form-data", handler.Request.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("name=picture", handler.RequestBody!);
        Assert.Contains("avatar.png", handler.RequestBody!);
        Assert.Equal("images/users/abc.webp", response.User!.ProfilePictureUrl);
    }

    [Fact]
    public async Task RemovePicture_UsesTheSameRouteAndProjectsANullPictureUrl()
    {
        var handler = new RecordingHandler(
            "{\"isComplete\":true,\"user\":{\"id\":\"" + MockUserId + "\",\"username\":\"mockuser\",\"profilePictureUrl\":null},\"email\":null,\"emailVerified\":false}");
        using var httpClient = CreateHttpClient(handler);
        var client = RestService.For<IUserClient>(httpClient, CreateRefitSettings());

        var response = await client.RemoveCurrentUserProfilePictureAsync();

        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
        Assert.Equal("/v1/lan/users/me/picture", handler.Request.RequestUri!.AbsolutePath);
        Assert.Null(response.User!.ProfilePictureUrl);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.example.test/") };

    private static RefitSettings CreateRefitSettings() => new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(JsonOptions)
    };

    private sealed class RecordingHandler(
        string responseBody = "{}",
        HttpStatusCode responseStatus = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(responseStatus)
            {
                RequestMessage = request,
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
