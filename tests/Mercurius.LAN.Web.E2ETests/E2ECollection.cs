namespace Mercurius.LAN.Web.E2ETests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class E2ECollection : ICollectionFixture<PlaywrightE2EFixture>
{
    public const string Name = "Playwright E2E";
}
