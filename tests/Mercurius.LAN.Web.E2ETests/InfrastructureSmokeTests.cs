using Microsoft.Playwright;

namespace Mercurius.LAN.Web.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class InfrastructureSmokeTests(PlaywrightE2EFixture app)
{
    [Fact]
    public async Task MemberSignsInThroughOidcAndLoadsProfileFromTheRealApi()
    {
        var persona = await app.CreatePersonaAsync("smoke-member");
        var context = await app.NewAuthenticatedContextAsync(persona);
        try
        {
            var page = context.Pages[0];
            await page.GotoAsync(new Uri(new Uri(app.BaseUrl), "profile").ToString());
            await page.WaitForInteractiveAsync();
            await Assertions.Expect(page.GetByLabel("Username", new() { Exact = true })).ToHaveValueAsync(persona.Username);
        }
        finally
        {
            await app.CloseContextAsync(context);
        }
    }
}
