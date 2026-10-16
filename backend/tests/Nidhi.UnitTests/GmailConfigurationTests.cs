using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nidhi.Api.Authentication;
using Xunit;

namespace Nidhi.UnitTests;

public sealed class GmailConfigurationTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Nondevelopment_rejects_disabled_revocation_checks(string environment)
    {
        var builder = Builder(environment, "false");
        Assert.Throws<InvalidOperationException>(() => builder.AddNidhiAuthentication());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Development_requires_explicit_opt_out(string? configured, bool expected)
    {
        var builder = Builder("Development", configured);
        builder.AddNidhiAuthentication();
        using var services = builder.Services.BuildServiceProvider();
        using var scope = services.CreateScope();
        var smtp = scope.ServiceProvider.GetRequiredService<ISmtpClient>();
        Assert.Equal(expected, smtp.CheckCertificateRevocation);
        Assert.Null(smtp.ServerCertificateValidationCallback);
    }

    private static WebApplicationBuilder Builder(string environment, string? configured)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentityEmail:Provider"] = "Gmail",
            ["IdentityEmail:Gmail:CheckCertificateRevocation"] = configured
        });
        return builder;
    }
}
