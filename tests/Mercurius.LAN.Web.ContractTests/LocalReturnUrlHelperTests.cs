using Mercurius.LAN.Web.Extensions;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class LocalReturnUrlHelperTests
{
    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/\n//evil.example")]
    [InlineData("/\t\\evil.example")]
    public void UnsafeReturnUrlsFallBackToHome(string returnUrl)
    {
        Assert.Equal("/", LocalReturnUrlHelper.GetSafeLocalReturnUrl(returnUrl));
    }

    [Fact]
    public void LocalReturnUrlIsPreserved()
    {
        Assert.Equal("/tournaments/abc?tab=matches#details",
            LocalReturnUrlHelper.GetSafeLocalReturnUrl("/tournaments/abc?tab=matches#details"));
    }

    [Theory]
    [InlineData("/profile")]
    [InlineData("/profile?tab=settings#security")]
    [InlineData("/profile/subpath")]
    [InlineData("/PROFILE/subpath")]
    [InlineData("/complete-profile")]
    [InlineData("/complete-profile/subpath")]
    [InlineData("/users/alice")]
    [InlineData("/USERS/alice")]
    [InlineData("/users/alice?tab=details#recent")]
    [InlineData("/%75sers/alice")]
    [InlineData("/users/%61lice")]
    [InlineData("/teams/manage")]
    [InlineData("/teams/manage/subpath")]
    [InlineData("/admin/sponsors")]
    [InlineData("/admin/sponsors/subpath")]
    [InlineData("/account/logout")]
    [InlineData("/pro%66ile")]
    [InlineData("/%70rofile")]
    [InlineData("/%70rofile/subpath")]
    [InlineData("/complete-%70rofile")]
    [InlineData("/complete-profile/%73ubpath")]
    [InlineData("/teams/%6danage")]
    [InlineData("/teams/manage/%73ubpath")]
    [InlineData("/admin/%73ponsors")]
    [InlineData("/admin/sponsors/%73ubpath")]
    [InlineData("/account/l%6fgout")]
    [InlineData("/account/logout/%73ubpath")]
    public void ProtectedLogoutReturnUrlsFallBackToHome(string returnUrl)
    {
        Assert.Equal("/", LocalReturnUrlHelper.GetSafeLogoutReturnUrl(returnUrl));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/\nevil.example")]
    [InlineData("/\tevil.example")]
    [InlineData("/foo\\..\\profile")]
    [InlineData("/foo/%5c..%5cprofile")]
    [InlineData("/foo/%255c..%255cprofile")]
    [InlineData("/foo/../profile")]
    [InlineData("/foo/%2e%2e/profile")]
    [InlineData("/foo/%252e%252e/profile")]
    [InlineData("/foo/%2f%2fevil.example")]
    [InlineData("/foo/%252f%252fevil.example")]
    [InlineData("/foo/%0a/profile")]
    [InlineData("/foo/%250a/profile")]
    [InlineData("/foo/%80/profile")]
    [InlineData("/foo/%C2%90/profile")]
    [InlineData("/foo/%")]
    [InlineData("/foo/%2g/profile")]
    public void MaliciousLogoutReturnUrlsFallBackToHome(string returnUrl)
    {
        Assert.Equal("/", LocalReturnUrlHelper.GetSafeLogoutReturnUrl(returnUrl));
    }

    [Theory]
    [InlineData("/tournaments/abc?tab=matches#details")]
    [InlineData("/public/path%20here?value=%2F#details")]
    [InlineData("/public/%66ile?value=%2F#details")]
    [InlineData("/public/%C3%A9")]
    [InlineData("/public?next=/profile&value=../profile#details")]
    public void PublicLogoutReturnUrlPreservesQueryAndFragment(string returnUrl)
    {
        Assert.Equal(returnUrl, LocalReturnUrlHelper.GetSafeLogoutReturnUrl(returnUrl));
    }

    [Fact]
    public void ExistingLocalReturnPolicyStillPreservesProtectedLoginDestination()
    {
        Assert.Equal("/profile?tab=settings#security",
            LocalReturnUrlHelper.GetSafeLocalReturnUrl("/profile?tab=settings#security"));
    }
}
