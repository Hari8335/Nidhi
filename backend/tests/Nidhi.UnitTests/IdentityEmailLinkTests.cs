using Microsoft.Extensions.Configuration;
using Nidhi.Infrastructure.Identity;
using Xunit;

namespace Nidhi.UnitTests;

public sealed class IdentityEmailLinkTests
{
    [Theory]
    [InlineData("http://untrusted.example")]
    [InlineData("https://nidhi.example/other")]
    [InlineData("https://name:password@nidhi.example")]
    [InlineData("https://nidhi.example?next=other")]
    [InlineData("javascript:alert(1)")]
    [InlineData(null)]
    public void Rejects_missing_or_unsafe_frontend_origins(string? origin)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["IdentityEmail:PublicBaseUrl"] = origin }).Build();
        Assert.Throws<InvalidOperationException>(() => IdentityEmailLink.Create(config, Guid.NewGuid(), "secret"));
    }

    [Fact]
    public void Tokens_are_escaped_and_only_in_the_fragment()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["IdentityEmail:PublicBaseUrl"] = "http://localhost:3000" }).Build();
        var link = new Uri(IdentityEmailLink.Create(config, Guid.NewGuid(), "a+/=b&c"));
        Assert.Equal("", link.Query);
        Assert.Contains("token=a%2B%2F%3Db%26c", link.Fragment);
    }
}
