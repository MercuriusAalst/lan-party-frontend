using Mercurius.LAN.Web.E2ETests.Infrastructure;

namespace Mercurius.LAN.Web.E2ETests;

public sealed class ManagedProcessTests
{
    [Theory]
    [InlineData("Now listening on: http://127.0.0.1:52792", "http://127.0.0.1:52792")]
    [InlineData("{\"Message\":\"Now listening on: http://127.0.0.1:52792\",\"State\":{\"address\":\"http://127.0.0.1:52792\"}}", "http://127.0.0.1:52792")]
    public void ListeningAddressMatchesPlainAndStructuredLogs(string line, string expectedAddress)
    {
        var match = ManagedProcess.ListeningAddress.Match(line);

        Assert.True(match.Success);
        Assert.Equal(expectedAddress, match.Groups[1].Value);
        Assert.True(Uri.TryCreate(match.Groups[1].Value, UriKind.Absolute, out _));
    }
}
