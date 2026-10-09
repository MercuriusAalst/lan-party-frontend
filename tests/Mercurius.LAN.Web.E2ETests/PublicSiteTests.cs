using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Npgsql;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Public shell: the anonymously reachable pages, their navigation entry points and
/// their visible loading / empty / error states.
/// </summary>
[Collection(E2ECollection.Name)]
public class PublicSiteTests(PlaywrightE2EFixture app) : E2ETestBase(app)
{
    [Fact]
    public async Task AnonymousHomePagePresentsEventFactsAndEntryPoints()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");

        await Expect(page).ToHaveTitleAsync("Mercurius LAN");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "A LAN with tournaments and community for students." }))
            .ToBeVisibleAsync();
        await Expect(page.GetByText("20 Nov 2026 - 21 Nov 2026")).ToBeVisibleAsync();
        await Expect(page.GetByText("HoGent Aalst, Arbeidstraat 14, 9300 Aalst, Belgium")).ToBeVisibleAsync();

        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Get Your Tickets" }))
            .ToHaveAttributeAsync("href", "/info#tickets");
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Event Info" }))
            .ToHaveAttributeAsync("href", "/info");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Featured tournaments" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "View all partners" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "View all tournaments" }))
            .ToHaveAttributeAsync("href", "/tournaments");

        // This test runs against the truncated per-test database, so the sponsor section is empty.
        await Expect(page.GetByText("Our event partners will appear here soon.")).ToBeVisibleAsync(new() { Timeout = 15000 });

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Log in" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Register" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task HomeTicketCallToActionNavigatesToTicketSection()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Link, new() { Name = "Get Your Tickets" }));

        await Expect(page).ToHaveURLAsync(new Regex("/info#tickets$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Pick your pass." })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task InfoPageRendersPackingTicketsMenuAndContactSections()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}info");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Everything for Mercurius LAN" })).ToBeVisibleAsync();
        await Expect(page.GetByText("Gamer passes from 10 EUR")).ToBeVisibleAsync();

        await Expect(page.Locator("#packing")).ToBeVisibleAsync();
        await Expect(page.GetByText("Power strip")).ToBeVisibleAsync();
        await Expect(page.GetByText("Max 2 monitors")).ToBeVisibleAsync();

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Pick your pass." })).ToBeVisibleAsync();
        await Expect(page.Locator(".lan-ticket--gamer")).ToContainTextAsync("Basic");
        await Expect(page.Locator(".lan-ticket--visitor")).ToContainTextAsync("5 EUR");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Our menu" })).ToBeVisibleAsync();
        await Expect(page.GetByText("Croque Monsieur")).ToBeVisibleAsync();

        await Expect(page.Locator("#contact-info")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Contact us" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Join the Mercurius Aalst Discord server" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task InfoNavigationDropdownJumpsToRequestedSection()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Info", Exact = true }));

        var dropdown = page.Locator(".info-dropdown");
        await Expect(dropdown).ToBeVisibleAsync();
        await page.ClickWhenInteractiveAsync(dropdown.GetByRole(AriaRole.Link, new() { Name = "What to bring" }));

        await Expect(page).ToHaveURLAsync(new Regex("/info#packing$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Pack list" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ContactFormRejectsBlankSubmissionWithoutSending()
    {
        app.ContactEmailSink.Reset();
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}info");
        await page.WaitForInteractiveAsync();
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Send message" }));

        await Expect(page.GetByText("Name is required.").First).ToBeVisibleAsync();
        await Expect(page.GetByText("Email or Discord is required.").First).ToBeVisibleAsync();
        await Expect(page.GetByText("Message is required.").First).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Send message" })).ToBeEnabledAsync();
        Assert.False(app.ContactEmailSink.HasReceivedMessage);
    }

    [Fact]
    public async Task ContactFormRejectsMessageShorterThanTenCharacters()
    {
        app.ContactEmailSink.Reset();
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}info");
        await page.WaitForInteractiveAsync();
        await page.FillAsync("#contact-name", "Robin Visitor");
        await page.FillAsync("#contact-contact", "robin@example.test");
        await page.FillAsync("#contact-message", "too short");
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Send message" }));

        await Expect(page.GetByText("Message must be at least 10 characters.").First).ToBeVisibleAsync();
        await Expect(page.GetByText("Name is required.")).ToHaveCountAsync(0);
        Assert.False(app.ContactEmailSink.HasReceivedMessage);
    }

    [Fact]
    public async Task ContactFormSurfacesMailTransportFailureForValidMessage()
    {
        app.ContactEmailSink.Reset();
        app.ContactEmailSink.RejectNextMessage();
        var receivedMessage = app.ContactEmailSink.WaitForMessageAsync();
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}info");
        await page.WaitForInteractiveAsync();
        await SubmitContactMessageAsync(page, "Robin Visitor", "robin@example.test", "Please tell me more about the LAN schedule and catering.");

        await Expect(page.GetByText("Your message could not be sent. Please try Discord, Facebook, or Instagram.").First).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Send message" })).ToBeEnabledAsync();
        await Expect(page.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("Robin Visitor");
        await Expect(page.GetByLabel("Email or Discord", new() { Exact = true })).ToHaveValueAsync("robin@example.test");
        await Expect(page.GetByLabel("Message", new() { Exact = true })).ToHaveValueAsync("Please tell me more about the LAN schedule and catering.");

        var message = await receivedMessage;
        Assert.False(message.Accepted);
        Assert.Contains("<lan-contact@recipient.test>", message.EnvelopeRecipients);

        // The sink rejects only the first message, so resubmitting the preserved draft must succeed and clear the form.
        await page.GetByRole(AriaRole.Button, new() { Name = "Send message", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Your message has been sent.").First).ToBeVisibleAsync();
        await Expect(page.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("");
        await Expect(page.GetByLabel("Email or Discord", new() { Exact = true })).ToHaveValueAsync("");
        await Expect(page.GetByLabel("Message", new() { Exact = true })).ToHaveValueAsync("");
    }

    [Fact]
    public async Task ContactFormSendsEmailWithReplyToAndClearsAfterSuccess()
    {
        app.ContactEmailSink.Reset();
        app.ContactEmailSink.HoldNextAcceptance();
        await using var context = await app.NewContextAsync();
        try
        {
            var receivedMessage = app.ContactEmailSink.WaitForMessageAsync();
            var page = await context.NewPageAsync();

            await page.GotoAsync($"{app.BaseUrl}info");
            await page.WaitForInteractiveAsync();
            await SubmitContactMessageAsync(page, "Robin Visitor", "robin@example.test", "LANContactInquiry12345");
            var message = await receivedMessage;
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sending...", Exact = true })).ToBeDisabledAsync();
            await Expect(page.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("Robin Visitor");
            await Expect(page.GetByLabel("Email or Discord", new() { Exact = true })).ToHaveValueAsync("robin@example.test");
            await Expect(page.GetByLabel("Message", new() { Exact = true })).ToHaveValueAsync("LANContactInquiry12345");
            app.ContactEmailSink.ReleaseAcceptance();
            await Expect(page.GetByText("Your message has been sent.").First).ToBeVisibleAsync();

            Assert.True(message.Accepted);
            Assert.Equal("<lan-contact@sender.test>", message.EnvelopeSender);
            Assert.Contains("<lan-contact@recipient.test>", message.EnvelopeRecipients);
            Assert.Equal("lan-contact@sender.test", new MailAddress(ReadSmtpHeader(message, "From")).Address);
            Assert.Equal("lan-contact@recipient.test", new MailAddress(ReadSmtpHeader(message, "To")).Address);
            Assert.Equal("robin@example.test", new MailAddress(ReadSmtpHeader(message, "Reply-To")).Address);
            Assert.Equal("Mercurius LAN contact: Robin Visitor", ReadSmtpHeader(message, "Subject"));
            Assert.Contains("LANContactInquiry12345", ReadSmtpBody(message));

            await Expect(page.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("");
            await Expect(page.GetByLabel("Email or Discord", new() { Exact = true })).ToHaveValueAsync("");
            await Expect(page.GetByLabel("Message", new() { Exact = true })).ToHaveValueAsync("");
        }
        finally
        {
            app.ContactEmailSink.ReleaseAcceptance();
        }
    }

    [Fact]
    public async Task ContactFormUsesSenderAsReplyToForDiscordContact()
    {
        app.ContactEmailSink.Reset();
        var receivedMessage = app.ContactEmailSink.WaitForMessageAsync();
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}info");
        await page.WaitForInteractiveAsync();
        await SubmitContactMessageAsync(page, "Robin Visitor", "Discord: robin#1234", "LANContactInquiry12345");

        await Expect(page.GetByText("Your message has been sent.").First).ToBeVisibleAsync();
        var message = await receivedMessage;

        Assert.True(message.Accepted);
        Assert.Equal("lan-contact@sender.test", new MailAddress(ReadSmtpHeader(message, "Reply-To")).Address);
        Assert.Contains("robin#1234", ReadSmtpBody(message));
        await Expect(page.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("");
    }

    private static async Task SubmitContactMessageAsync(IPage page, string name, string contact, string message)
    {
        await page.GetByLabel("Name", new() { Exact = true }).FillAsync(name);
        await page.GetByLabel("Email or Discord", new() { Exact = true }).FillAsync(contact);
        await page.GetByLabel("Message", new() { Exact = true }).FillAsync(message);
        await page.GetByRole(AriaRole.Button, new() { Name = "Send message", Exact = true }).ClickAsync();
    }

    private static string ReadSmtpHeader(CapturedSmtpMessage message, string name)
    {
        var headerEnd = message.Data.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        Assert.True(headerEnd >= 0, "The SMTP message has no header/body separator.");
        var unfoldedHeaders = Regex.Replace(message.Data[..headerEnd], "\r\n[ \\t]+", " ");
        var header = unfoldedHeaders.Split("\r\n")
            .Single(line => line.StartsWith($"{name}:", StringComparison.OrdinalIgnoreCase));
        return header[(name.Length + 1)..].Trim();
    }

    // The transport encodes the body (quoted-printable for these ASCII payloads), so assert on the
    // decoded text rather than on wire bytes that may carry soft line breaks.
    private static string ReadSmtpBody(CapturedSmtpMessage message)
    {
        var headerEnd = message.Data.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        Assert.True(headerEnd >= 0, "The SMTP message has no header/body separator.");
        var body = message.Data[(headerEnd + 4)..];
        var decoded = ReadSmtpHeader(message, "Content-Transfer-Encoding") switch
        {
            var encoding when encoding.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase)
                => DecodeQuotedPrintable(body),
            var encoding when encoding.Equals("base64", StringComparison.OrdinalIgnoreCase)
                => Encoding.ASCII.GetString(Convert.FromBase64String(body)),
            _ => body
        };

        return decoded.Replace("\r\n", "\n");
    }

    private static string DecodeQuotedPrintable(string body)
    {
        var bytes = new List<byte>(body.Length);
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] != '=')
            {
                bytes.Add((byte)body[i]);
                continue;
            }

            if (i + 2 < body.Length && body[i + 1] == '\r' && body[i + 2] == '\n')
            {
                i += 2;
                continue;
            }

            if (i + 2 < body.Length && Uri.IsHexDigit(body[i + 1]) && Uri.IsHexDigit(body[i + 2]))
            {
                bytes.Add(Convert.ToByte(body.Substring(i + 1, 2), 16));
                i += 2;
                continue;
            }

            bytes.Add((byte)body[i]);
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    [Fact]
    public async Task SponsorsPageRendersSeededTiersInOrderWithTierSpecificDescriptions()
    {
        var admin = await app.CreatePersonaAsync("sponsors-page-admin", isAdmin: true);
        using var adminApi = app.CreateApiClient(admin);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var sponsors = new (string Tier, string Name, string Description)[]
        {
            ("Presenting", $"E2E Presenting {suffix}", $"presenting-description-{suffix}"),
            ("Gold", $"E2E Gold {suffix}", $"gold-description-{suffix}"),
            ("Silver", $"E2E Silver {suffix}", $"silver-description-{suffix}"),
            ("Bronze", $"E2E Bronze {suffix}", $"bronze-description-{suffix}")
        };
        foreach (var sponsor in sponsors)
        {
            await TournamentE2E.CreateSponsorAsync(
                adminApi,
                sponsor.Name,
                sponsor.Tier,
                "https://example.test/e2e-sponsor",
                sponsor.Description);
        }

        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}sponsors");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Why sponsors matter to the event" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Stronger tournament experience" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Become a Sponsor" }).First).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "We are currently shaping the partner lineup." }))
            .ToHaveCountAsync(0);

        foreach (var sponsor in sponsors)
        {
            var card = page.Locator("#current-partners .sponsor-card").Filter(new() { HasText = sponsor.Name });
            await Expect(card).ToBeVisibleAsync();
            await Expect(card.GetByRole(AriaRole.Heading, new() { Name = sponsor.Name })).ToBeVisibleAsync();
            await Expect(card.GetByAltText($"{sponsor.Name} logo")).ToBeVisibleAsync();
            await Expect(card.GetByText("Visit website")).ToBeVisibleAsync();
            await Expect(card).ToHaveAttributeAsync("href", "https://example.test/e2e-sponsor");
            await Expect(card).ToHaveAttributeAsync("aria-label", $"Visit {sponsor.Name}");
        }

        // Only the Presenting and Gold tiers publish the partner description.
        foreach (var sponsor in sponsors.Where(item => item.Tier is "Presenting" or "Gold"))
            await Expect(page.GetByText(sponsor.Description, new() { Exact = true })).ToBeVisibleAsync();
        foreach (var sponsor in sponsors.Where(item => item.Tier is "Silver" or "Bronze"))
            await Expect(page.GetByText(sponsor.Description, new() { Exact = true })).ToHaveCountAsync(0);

        // Tier sections render in the fixed Presenting -> Gold -> Silver -> Bronze order.
        var tierHeadings = await page.Locator("#current-partners .sponsors-tier-header h2").AllTextContentsAsync();
        var expectedOrder = new[] { "Presenting Partners", "Gold Partners", "Silver Partners", "Bronze Partners" };
        var positions = expectedOrder.Select(label => tierHeadings.ToList().IndexOf(label)).ToArray();
        Assert.All(positions, position => Assert.True(position >= 0,
            $"Missing tier heading. Rendered: {string.Join(" | ", tierHeadings)}"));
        Assert.True(positions.SequenceEqual(positions.OrderBy(position => position)),
            $"Tier sections rendered out of order: {string.Join(" | ", tierHeadings)}");
    }

    [Fact]
    public async Task PrivacyPolicyPageDescribesCollectedDataAndRights()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}privacy-policy");

        await Expect(page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Privacy Policy for LAN-Party Mercurius Aalst and https://lan.mercurius-aalst.be"
        })).ToBeVisibleAsync();
        await Expect(page.GetByText("Edition: 2025-2026")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Data controller" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Which data do we collect and when?" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Retention period" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your rights and complaints" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "HoGent Privacy Policy" }))
            .ToHaveAttributeAsync("href", "https://www.hogent.be/privacyverklaring-hogent/");
    }

    [Fact]
    public async Task UnknownRouteShowsNotFoundPageForAnonymousVisitor()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{app.BaseUrl}route-that-does-not-exist");

        // Observed on .NET 10 static SSR: GET /route-that-does-not-exist answers 404 with an empty
        // document instead of the router's NotFound StatusPage (trace 008.zip for the authenticated
        // twin: 404, DOM has no .status-page). Kept as the correct expectation.
        Assert.Equal((int)HttpStatusCode.NotFound, response!.Status);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Page Not Found" })).ToBeVisibleAsync();
        await Expect(page.GetByText("The page you requested does not exist or has moved.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Back Home" })).ToHaveAttributeAsync("href", "/");
    }

    [Fact]
    public async Task ErrorPageRendersSystemFailureState()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}Error");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Error" })).ToBeVisibleAsync();
        await Expect(page.GetByText("An unexpected error occurred while processing your request.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Back Home" })).ToHaveAttributeAsync("href", "/");
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Browse Tournaments" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LanguageSelectorSwitchesCultureToDutchAndItSurvivesReload()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Log in" })).ToBeVisibleAsync();

        await page.WaitForInteractiveAsync();
        await page.SelectOptionAsync("#footer-language-select", "nl-BE");

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Inloggen" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Registreren" })).ToBeVisibleAsync();

        await page.ReloadAsync();
        await Expect(page.Locator("#footer-language-select")).ToHaveValueAsync("nl-BE");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Inloggen" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Ontdek en speel" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ThemeToggleFlipsTheLayoutThemeAndItSurvivesReload()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");
        var shell = page.Locator(".layout-shell");
        await Expect(shell).ToHaveAttributeAsync("data-theme-state", "light");

        await page.ClickWhenInteractiveAsync(page.Locator(".theme-toggle"));

        await Expect(shell).ToHaveAttributeAsync("data-theme-state", "dark");
        // The pressed state is an ARIA boolean and must be exposed literally as "true".
        await Expect(page.Locator(".theme-toggle")).ToHaveAttributeAsync("aria-pressed", "true");

        await page.ReloadAsync();
        await Expect(shell).ToHaveAttributeAsync("data-theme-state", "dark");
        await Expect(page.Locator(".theme-toggle")).ToHaveAttributeAsync("aria-pressed", "true");
    }

    [Fact]
    public async Task MobileNavigationOverlayOpensAndClosesFromButtonBackdropAndEscape()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync($"{app.BaseUrl}");

        var toggle = page.GetByRole(AriaRole.Button, new() { Name = "Toggle navigation" });
        var panel = page.Locator("#primary-navigation");
        var backdrop = page.Locator(".brand-nav-backdrop");

        await page.ClickWhenInteractiveAsync(toggle);
        await Expect(panel).ToHaveClassAsync(new Regex(@"\bopen\b"));
        await Expect(backdrop).ToBeVisibleAsync();

        // The sticky header overlays the backdrop's top edge and intercepts pointer events there,
        // so dismiss through a point that is unambiguously over the backdrop.
        await page.Mouse.ClickAsync(40, 700);
        await Expect(panel).Not.ToHaveClassAsync(new Regex(@"\bopen\b"));

        await page.ClickWhenInteractiveAsync(toggle);
        await Expect(panel).ToHaveClassAsync(new Regex(@"\bopen\b"));
        await page.Keyboard.PressAsync("Escape");
        await Expect(panel).Not.ToHaveClassAsync(new Regex(@"\bopen\b"));
        await Expect(toggle).ToBeFocusedAsync();
    }

    [Fact]
    public async Task GlobalSearchReportsNoMatchesForAnUnknownQuery()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");
        await page.WaitForInteractiveAsync();
        await page.FillAsync("#global-nav-search", "zzzzzznotpresent");

        await Expect(page.GetByText("No matches found.")).ToBeVisibleAsync();
        await Expect(page.Locator("#global-nav-search")).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(page.Locator("#global-nav-search-results")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task GlobalSearchClearButtonResetsTheQueryAndClosesResults()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");
        await page.WaitForInteractiveAsync();
        await page.FillAsync("#global-nav-search", "zzzzzznotpresent");
        await Expect(page.Locator("#global-nav-search-results")).ToBeVisibleAsync();

        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Clear search" }));

        await Expect(page.Locator("#global-nav-search")).ToHaveValueAsync(string.Empty);
        await Expect(page.Locator("#global-nav-search-results")).ToHaveCountAsync(0);
        await Expect(page.Locator("#global-nav-search")).ToHaveAttributeAsync("aria-expanded", "false");
    }

    [Fact]
    public async Task GlobalSearchEscapeDismissesResults()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");
        await page.WaitForInteractiveAsync();
        await page.FillAsync("#global-nav-search", "zzzzzznotpresent");
        await Expect(page.Locator("#global-nav-search-results")).ToBeVisibleAsync();

        await page.PressAsync("#global-nav-search", "Escape");

        await Expect(page.Locator("#global-nav-search-results")).ToHaveCountAsync(0);
        await Expect(page.Locator("#global-nav-search")).ToHaveAttributeAsync("aria-expanded", "false");
        // The input is <input type="search">, and Chromium clears those natively on Escape, so the
        // retained query value is browser behaviour rather than an application contract.
    }

    [Fact]
    public async Task InfoMenuClosesWhenTheInteractionOverlayIsClicked()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Info", Exact = true }));
        await Expect(page.Locator(".info-dropdown")).ToBeVisibleAsync();

        await page.ClickWhenInteractiveAsync(page.Locator(".nav-overlay"));

        await Expect(page.Locator(".info-dropdown")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task FooterQuickLinksNavigateToTheLegalPage()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");
        await page.ClickWhenInteractiveAsync(
            page.Locator("footer.footer").GetByRole(AriaRole.Link, new() { Name = "Privacy Policy" }));

        await Expect(page).ToHaveURLAsync(new Regex("/privacy-policy$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Data controller" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task HomeFeaturedTournamentsMirrorThePublishedOrderAndOpenTheirDetailPage()
    {
        var admin = await app.CreatePersonaAsync("home-featured-admin", isAdmin: true);
        using var adminApi = app.CreateApiClient(admin);
        var seededName = TournamentE2E.Unique("E2E Home Featured");
        var seededId = await TournamentE2E.CreateTournamentAsync(adminApi, seededName, "SingleElimination", "Individual");

        // The home page asks for the first page of tournaments; mirror that exact request and
        // compare the rendered order against it instead of assuming which tournaments exist.
        using var response = await app.Api.GetAsync("v1/lan/tournaments?pageSize=12");
        var published = await TournamentE2E.ReadJsonAsync(response);
        var expected = published.EnumerateArray()
            .Select(item => (Id: item.GetProperty("id").GetGuid(), Name: item.GetProperty("name").GetString()!))
            .Take(4)
            .ToList();
        Assert.NotEmpty(expected);

        // The seeded tournament is the guarantee that the featured section has content to render.
        var seeded = await TournamentE2E.ReadJsonAsync(await app.Api.GetAsync($"v1/lan/tournaments/{seededId}"));
        Assert.Equal(seededName, seeded.GetProperty("name").GetString());

        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");

        var lead = page.Locator(".home-tournament-lead");
        await Expect(lead).ToContainTextAsync(expected[0].Name, new() { Timeout = 15000 });
        for (var index = 1; index < expected.Count; index++)
        {
            await Expect(page.Locator(".home-tournament-row").Nth(index - 1))
                .ToContainTextAsync(expected[index].Name, new() { Timeout = 15000 });
        }

        await page.ClickWhenInteractiveAsync(lead);

        await Expect(page).ToHaveURLAsync(new Regex($"/tournaments/{expected[0].Id}$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = expected[0].Name, Level = 1 })).ToBeVisibleAsync();
        await Expect(page).ToHaveTitleAsync(new Regex(Regex.Escape(expected[0].Name)));
    }

    [Fact]
    public async Task GlobalSearchIgnoresQueriesShorterThanThreeCharacters()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{app.BaseUrl}");
        await page.WaitForInteractiveAsync();

        await page.FillAsync("#global-nav-search", "zzzzzznotpresent");
        await Expect(page.Locator("#global-nav-search")).ToHaveAttributeAsync("aria-expanded", "true");

        await page.FillAsync("#global-nav-search", "ab");

        await Expect(page.Locator("#global-nav-search-results")).ToHaveCountAsync(0);
        await Expect(page.Locator("#global-nav-search")).ToHaveAttributeAsync("aria-expanded", "false");
    }

    [Fact]
    public async Task HomePageShowsTheEmptyTournamentStateWhenNoTournamentExists()
    {
        // Per-test isolation truncates every table before this test, so there are no tournaments
        // yet. The empty state must render its own copy and no featured cards.
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");

        await Expect(page.GetByText("No tournaments are currently published.", new() { Exact = true }))
            .ToBeVisibleAsync();
        await Expect(page.Locator(".home-tournament-lead")).ToHaveCountAsync(0);
        await Expect(page.Locator(".home-tournament-row")).ToHaveCountAsync(0);

        // Navigating to the shell must offer the browse-tournaments entry point instead.
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "View all tournaments" }))
            .ToHaveAttributeAsync("href", "/tournaments");

        // A second visit proves the empty state is a stable rendering, not a transient load screen.
        await page.ReloadAsync();
        await Expect(page.GetByText("No tournaments are currently published.", new() { Exact = true }))
            .ToBeVisibleAsync();
        await Expect(page.Locator(".home-tournament-lead")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SponsorsPageShowsTheEmptyStateWhenNoSponsorExists()
    {
        // Per-test isolation truncates every table before this test, so there are no sponsors yet.
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}sponsors");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "We are currently shaping the partner lineup." }))
            .ToBeVisibleAsync();
        await Expect(page.GetByText("Want your brand featured at the next Mercurius LAN? Get in touch and let's build something great together."))
            .ToBeVisibleAsync();
        await Expect(page.Locator("#current-partners")).ToHaveCountAsync(0);
        await Expect(page.Locator("#current-partners .sponsor-card")).ToHaveCountAsync(0);
        await Expect(page.Locator("#current-partners .sponsors-tier-header")).ToHaveCountAsync(0);

        // The empty state routes visitors to the two sensible next steps.
        var emptyActions = page.Locator(".sponsors-empty-actions");
        await Expect(emptyActions.GetByRole(AriaRole.Link, new() { Name = "Become a Sponsor" }))
            .ToHaveAttributeAsync("href", "/#contact");
        await Expect(emptyActions.GetByRole(AriaRole.Link, new() { Name = "View Event Info" }))
            .ToHaveAttributeAsync("href", "/info");

        // The impact/why section above the empty state is still rendered.
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Why sponsors matter to the event" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Stronger tournament experience" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "More comfort at the LAN" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Community momentum" })).ToBeVisibleAsync();

        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "We are currently shaping the partner lineup." }))
            .ToBeVisibleAsync();
        await Expect(page.Locator("#current-partners .sponsor-card")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task HomeTournamentLoadFailureShowsTheRetryStateAndRecoversAfterTheApiIsRestored()
    {
        var admin = await app.CreatePersonaAsync("home-fault-admin", isAdmin: true);
        using var adminApi = app.CreateApiClient(admin);
        var tournamentName = TournamentE2E.Unique("E2E Home Retry");
        await TournamentE2E.CreateTournamentAsync(adminApi, tournamentName, "SingleElimination", "Individual");

        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");

        // Baseline: the seeded tournament renders on the home page.
        var lead = page.Locator(".home-tournament-lead");
        await Expect(lead).ToContainTextAsync(tournamentName, new() { Timeout = 15000 });

        // Make the API's tournament read fail while the fault is installed, then trigger a real
        // server-side Refit call by reloading the page in the same browser.
        await using (var fault = await DatabaseReadFault.InstallAsync(app, "tournaments"))
        {
            await page.ReloadAsync();
            await Expect(page.GetByText("Could not load the tournament highlights right now.")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Try again" })).ToBeVisibleAsync();
            await Expect(page.GetByText(tournamentName)).ToHaveCountAsync(0);
        }

        // With the fault removed, Retry must reuse the same component and recover in place.
        await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Try again" }));
        await Expect(page.GetByText("Could not load the tournament highlights right now.")).ToHaveCountAsync(0);
        await Expect(lead).ToContainTextAsync(tournamentName, new() { Timeout = 15000 });
    }

    [Fact]
    public async Task GlobalSearchKeyboardNavigationHighlightsResultsAndEnterOpensTheTournament()
    {
        var admin = await app.CreatePersonaAsync("search-keyboard-admin", isAdmin: true);
        using var adminApi = app.CreateApiClient(admin);
        // A space-free token keeps the query valid whatever the search normalizer does with
        // whitespace, and ties both seeded tournaments to one deterministic result set.
        var token = Guid.NewGuid().ToString("N")[..8];
        var alphaName = $"E2EKeyboard{token}Alpha";
        var betaName = $"E2EKeyboard{token}Beta";
        var alphaId = await TournamentE2E.CreateTournamentAsync(adminApi, alphaName, "SingleElimination", "Individual");
        await TournamentE2E.CreateTournamentAsync(adminApi, betaName, "SingleElimination", "Individual");
        await WaitForSearchResultAsync(
            app,
            token,
            item => item.TryGetProperty("tournamentId", out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    value.GetGuid() == alphaId);
        await WaitForSearchResultAsync(
            app,
            token,
            item => item.TryGetProperty("displayLabel", out var value) &&
                    string.Equals(value.GetString(), betaName, StringComparison.OrdinalIgnoreCase));

        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");
        await page.WaitForInteractiveAsync();
        await page.FillAsync("#global-nav-search", token);

        var options = page.Locator("#global-nav-search-results button[role='option']");
        var alpha = options.Filter(new() { HasText = alphaName });
        var beta = options.Filter(new() { HasText = betaName });
        await Expect(alpha).ToBeVisibleAsync();
        await Expect(beta).ToBeVisibleAsync();

        // A listbox option must expose its selection as the literal ARIA boolean "true".
        // ArrowUp from "nothing selected" wraps to the last result, ArrowDown from the last wraps
        // back to the first, then stepping down/up moves one result at a time.
        await page.PressAsync("#global-nav-search", "ArrowUp");
        await Expect(beta).ToHaveAttributeAsync("aria-selected", "true");
        await page.PressAsync("#global-nav-search", "ArrowDown");
        await Expect(alpha).ToHaveAttributeAsync("aria-selected", "true");
        await page.PressAsync("#global-nav-search", "ArrowDown");
        await Expect(beta).ToHaveAttributeAsync("aria-selected", "true");
        await page.PressAsync("#global-nav-search", "ArrowUp");
        await Expect(alpha).ToHaveAttributeAsync("aria-selected", "true");

        await page.PressAsync("#global-nav-search", "Enter");

        await Expect(page).ToHaveURLAsync(new Regex($"/tournaments/{alphaId}$"));
    }

    [Fact]
    public async Task GlobalSearchTeamResultOpensThePublicTeamProfile()
    {
        var captain = await app.CreatePersonaAsync("search-team-captain");
        using var captainApi = app.CreateApiClient(captain);
        var teamName = TeamE2E.UniqueTeamName();
        await TeamE2E.CreateTeamAsync(captainApi, teamName);
        await WaitForSearchResultAsync(
            app,
            teamName,
            item => item.TryGetProperty("teamName", out var value) &&
                    string.Equals(value.GetString(), teamName, StringComparison.OrdinalIgnoreCase));

        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{app.BaseUrl}");
        await page.WaitForInteractiveAsync();
        await page.FillAsync("#global-nav-search", teamName);

        var option = page.Locator("#global-nav-search-results button[role='option']").Filter(new() { HasText = teamName });
        await Expect(option).ToBeVisibleAsync();

        await page.PressAsync("#global-nav-search", "ArrowDown");
        await Expect(option).ToHaveAttributeAsync("aria-selected", "true");
        await page.PressAsync("#global-nav-search", "Enter");

        await Expect(page).ToHaveURLAsync(new Regex($"/teams/{Regex.Escape(Uri.EscapeDataString(teamName))}$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = teamName, Level = 1 })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TournamentsOverviewLoadFailureShowsTheErrorStateAndRecoversOnRetry()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        var statusSurface = page.Locator(".status-page");

        await using (var fault = await DatabaseReadFault.InstallAsync(app, "tournaments"))
        {
            await page.GotoAsync($"{app.BaseUrl}tournaments");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "We couldn't load the tournaments" }))
                .ToBeVisibleAsync(new() { Timeout = 15000 });
            // The same copy is also shown as a Blazored toast, so scope to the inline status surface.
            await Expect(statusSurface
                    .GetByText("We couldn't load the tournament list right now. Please try again in a moment."))
                .ToBeVisibleAsync();
            await Expect(statusSurface).ToHaveAttributeAsync("role", "alert");
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Try again" })).ToBeVisibleAsync();
        }

        // Hold the retry's real load open so the pending presentation is observable: the status
        // surface is replaced by the page's own loading indicator while the load is in flight.
        var hold = await HoldTournamentsTableAsync();
        try
        {
            await page.ClickWhenInteractiveAsync(page.GetByRole(AriaRole.Button, new() { Name = "Try again" }));

            var pending = page.Locator(".loading-overlay");
            await Expect(pending).ToBeVisibleAsync();
            await Expect(pending).ToHaveAttributeAsync("aria-busy", "true");
            await Expect(pending).ToContainTextAsync("Loading tournaments...");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "We couldn't load the tournaments" }))
                .ToHaveCountAsync(0);
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Try again" })).ToHaveCountAsync(0);
        }
        finally
        {
            // Disposing the connection rolls the locking transaction back and releases the load.
            await hold.DisposeAsync();
        }

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "We couldn't load the tournaments" }))
            .ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Tournaments", Level = 1 }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    /// <summary>
    /// Blocks other sessions from reading the tournament table so the page's retry request stays in
    /// flight. Disposing the connection rolls the locking transaction back.
    /// </summary>
    private async Task<NpgsqlConnection> HoldTournamentsTableAsync()
    {
        var connection = new NpgsqlConnection(app.DatabaseConnectionString);
        try
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "BEGIN; SET LOCAL lock_timeout = '15s'; LOCK TABLE tournament.tournaments IN ACCESS EXCLUSIVE MODE";
            await command.ExecuteNonQueryAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    [Fact]
    public async Task SponsorsPageLoadFailureReportsFailureToastAndRecoversOnReload()
    {
        var admin = await app.CreatePersonaAsync("sponsors-fault-admin", isAdmin: true);
        using var adminApi = app.CreateApiClient(admin);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var sponsorName = $"E2E Fault Sponsor {suffix}";
        await TournamentE2E.CreateSponsorAsync(
            adminApi,
            sponsorName,
            "Gold",
            "https://example.test/e2e-fault-sponsor",
            $"fault-description-{suffix}");

        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();
        var sponsorCard = page.Locator("#current-partners .sponsor-card").Filter(new() { HasText = sponsorName });

        await using (var fault = await DatabaseReadFault.InstallAsync(app, "sponsors"))
        {
            await page.GotoAsync($"{app.BaseUrl}sponsors");

            await Expect(page.GetByText("Failed to load sponsors.")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Expect(sponsorCard).ToHaveCountAsync(0);
        }

        await page.ReloadAsync();

        await Expect(sponsorCard).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(page.GetByText("Failed to load sponsors.")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task GlobalSearchFailureShowsTheUnavailableStateAndRecoversWhenTheIndexReturns()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await using (var fault = await DatabaseReadFault.InstallAsync(app, "search_documents"))
        {
            await page.GotoAsync($"{app.BaseUrl}");
            await page.WaitForInteractiveAsync();
            await page.FillAsync("#global-nav-search", "zzzzzznotpresent");

            await Expect(page.GetByText("Search is unavailable right now.")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Expect(page.GetByText("No matches found.")).ToHaveCountAsync(0);
        }

        await page.FillAsync("#global-nav-search", "zzzzzznotpresent2");

        await Expect(page.GetByText("No matches found.")).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(page.GetByText("Search is unavailable right now.")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task HomeSponsorFailureDegradesOnlyTheSponsorSection()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await using (var fault = await DatabaseReadFault.InstallAsync(app, "sponsors"))
        {
            await page.GotoAsync($"{app.BaseUrl}");
            await page.WaitForInteractiveAsync();

            await Expect(page.Locator("#home-sponsors")
                    .GetByText("Event partners are temporarily unavailable. The rest of the page is still available."))
                .ToBeVisibleAsync(new() { Timeout = 15000 });

            // The rest of the page is unaffected by the sponsor outage.
            await Expect(page.GetByRole(AriaRole.Heading, new()
                { Name = "A LAN with tournaments and community for students." })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Featured tournaments" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Get Your Tickets" })).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task TournamentsOverviewSponsorFailureDegradesOnlyTheSponsorSection()
    {
        await using var context = await app.NewContextAsync();
        var page = await context.NewPageAsync();

        await using (var fault = await DatabaseReadFault.InstallAsync(app, "sponsors"))
        {
            await page.GotoAsync($"{app.BaseUrl}tournaments");
            await page.WaitForInteractiveAsync();

            await Expect(page.GetByRole(AriaRole.Alert)
                    .GetByText("Event partners are temporarily unavailable. The rest of the page is still available."))
                .ToBeVisibleAsync(new() { Timeout = 15000 });

            // The tournament browse content still renders while the sponsor strip is degraded.
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Tournaments", Level = 1 })).ToBeVisibleAsync();
            await Expect(page.GetByText("Pick a tournament, build your lineup, and follow every match through to the final."))
                .ToBeVisibleAsync();
        }
    }

    /// <summary>
    /// The community search index is an eventually consistent outbox projection maintained by a
    /// background worker, so wait for the real search endpoint to expose the seeded entity before
    /// asserting what the browser does with that result.
    /// </summary>
    internal static async Task<JsonElement> WaitForSearchResultAsync(
        PlaywrightE2EFixture app,
        string query,
        Func<JsonElement, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var response = await app.Api.GetAsync($"v1/lan/search?query={Uri.EscapeDataString(query)}");
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (document.RootElement.TryGetProperty("results", out var results))
                {
                    foreach (var item in results.EnumerateArray())
                    {
                        if (predicate(item))
                            return item.Clone();
                    }
                }
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"The discovery search index did not expose a result for '{query}' within 30 seconds.");
    }

}
