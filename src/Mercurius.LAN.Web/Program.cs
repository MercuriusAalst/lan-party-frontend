using Auth0.AspNetCore.Authentication;
using Blazored.Toast;
using Mercurius.LAN.Web.Components;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Middleware;
#if INCLUDE_MOCK_BACKEND
using Mercurius.LAN.Web.Mock;
#endif
using Mercurius.LAN.Web.Options;
using Mercurius.LAN.Web.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor.Services;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
#if INCLUDE_MOCK_BACKEND
// Local overrides, including the mock backend switch, are only read in the allowed development environment.
if(MockBackendMode.IsAllowedEnvironment(builder.Environment))
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
#endif

const bool mockBackendIncluded =
#if INCLUDE_MOCK_BACKEND
    true;
#else
    false;
#endif
var mockModeEnabled = MockBackendMode.Resolve(
    builder.Configuration,
    builder.Environment,
    mockBackendIncluded);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazoredToast();
builder.Services.AddMudServices();
builder.Services.AddAntiforgery();
var jsonOptions = new JsonSerializerOptions
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter(), new LocalDateTimeJsonConverter() },
    AllowOutOfOrderMetadataProperties = true
};

builder.Services.AddCustomOptions(builder.Configuration, mockModeEnabled);
builder.Services.AddAuthenticationServices(builder.Configuration, mockModeEnabled);
builder.Services.AddHttpClients(jsonOptions, builder.Configuration, mockModeEnabled);
builder.Services.AddCustomServices(builder.Configuration, mockModeEnabled);

var app = builder.Build();

// Configure the HTTP request pipeline.
if(!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/status/{0}");

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

#if INCLUDE_MOCK_BACKEND
if(mockModeEnabled)
{
    app.MapGet("/account/login", async (HttpContext httpContext, MockBackendStore store, string? returnUrl = null, string? persona = null) =>
    {
        var mockOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MockBackendOptions>>().Value;
        var resolvedPersona = DependencyExtensions.NormalizeMockPersona(persona, mockOptions.Persona);
        var principal = DependencyExtensions.BuildMockPrincipal(resolvedPersona, store);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { RedirectUri = LocalReturnUrlHelper.GetSafeLocalReturnUrl(returnUrl) });

        return Results.LocalRedirect(LocalReturnUrlHelper.GetSafeLocalReturnUrl(returnUrl));
    }).AllowAnonymous();

    app.MapGet("/account/register", async (HttpContext httpContext, MockBackendStore store, string? returnUrl = null, string? persona = null) =>
    {
        var mockOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MockBackendOptions>>().Value;
        var resolvedPersona = DependencyExtensions.NormalizeMockPersona(persona, mockOptions.Persona);
        var principal = DependencyExtensions.BuildMockPrincipal(resolvedPersona, store);
        var redirectUri = BuildRegistrationRedirectUri(returnUrl);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { RedirectUri = redirectUri });

        return Results.LocalRedirect(redirectUri);
    }).AllowAnonymous();

    app.MapGet("/account/logout", async (HttpContext httpContext, string? returnUrl = null) =>
    {
        var redirectUri = LocalReturnUrlHelper.GetSafeLogoutReturnUrl(returnUrl);
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.LocalRedirect(redirectUri);
    }).AllowAnonymous();
}
else
#endif
{
    app.MapGet("/account/login", async (HttpContext httpContext, string? returnUrl = null) =>
    {
        var redirectUri = LocalReturnUrlHelper.GetSafeLocalReturnUrl(returnUrl);
        var authenticationProperties = new LoginAuthenticationPropertiesBuilder()
                .WithRedirectUri(redirectUri)
                .Build();

        await httpContext.ChallengeAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
    }).AllowAnonymous();

    app.MapGet("/account/register", async (HttpContext httpContext, string? returnUrl = null) =>
    {
        var redirectUri = BuildRegistrationRedirectUri(returnUrl);
        var authenticationProperties = new LoginAuthenticationPropertiesBuilder()
                .WithRedirectUri(redirectUri)
                .WithParameter("screen_hint", "signup")
                .Build();

        await httpContext.ChallengeAsync(Auth0Constants.AuthenticationScheme, authenticationProperties);
    }).AllowAnonymous();

    app.MapGet("/account/logout", async (HttpContext httpContext, string? returnUrl = null) =>
    {
        await AccountLogoutFlow.BeginLiveAsync(httpContext, returnUrl, app.Logger);
    }).RequireAuthorization();

    app.MapGet(AccountLogoutFlow.LogoutCallbackPath, (HttpRequest request) =>
    {
        var returnUrl = request.Query["returnUrl"];
        return Results.LocalRedirect(returnUrl.Count == 1
            ? LocalReturnUrlHelper.GetSafeLogoutReturnUrl(returnUrl[0])
            : "/");
    }).AllowAnonymous();
}

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string BuildRegistrationRedirectUri(string? returnUrl)
{
    var safeReturnUrl = LocalReturnUrlHelper.GetSafeLocalReturnUrl(returnUrl);
    return QueryHelpers.AddQueryString(safeReturnUrl, "registration", "true");
}
