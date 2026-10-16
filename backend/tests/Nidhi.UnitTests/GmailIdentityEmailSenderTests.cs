using System.Net;
using System.Text;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Nidhi.Application.Authentication;
using Nidhi.Infrastructure.Identity;
using Xunit;

namespace Nidhi.UnitTests;

public sealed class GmailIdentityEmailSenderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sends_account_bound_codes_over_required_tls(bool reset)
    {
        using var smtp = new CapturingSmtp();
        var sender = Sender(smtp);
        var id = Guid.NewGuid();
        const string token = "code-with-+/=characters";
        if (reset) await sender.SendPasswordResetAsync(id, "customer@example.test", token);
        else await sender.SendVerificationAsync(id, "customer@example.test", token);
        Assert.Equal("smtp.gmail.com", smtp.Host);
        Assert.Equal(587, smtp.Port);
        Assert.Equal(SecureSocketOptions.StartTls, smtp.Security);
        Assert.Equal("sender@gmail.com", smtp.Username);
        Assert.Equal("abcdefghijklmnop", smtp.Password);
        Assert.NotNull(smtp.Message);
        Assert.Equal("customer@example.test", smtp.Message.To.Mailboxes.Single().Address);
        Assert.Equal("sender@gmail.com", smtp.Message.From.Mailboxes.Single().Address);
        Assert.Contains(id.ToString(), smtp.Message.TextBody);
        Assert.Contains(Uri.EscapeDataString(token), smtp.Message.TextBody);
        Assert.Contains("one hour", smtp.Message.TextBody);
        Assert.Contains(reset ? "/reset-password#" : "/verify-email#", smtp.Message.TextBody);
        Assert.Contains(Uri.EscapeDataString(token), smtp.Message.HtmlBody);
        Assert.DoesNotContain("Account ID:", smtp.Message.TextBody);
        Assert.DoesNotContain("abcdefghijklmnop", smtp.Message.TextBody);
        Assert.Equal(reset ? "Reset your Nidhi password" : "Verify your Nidhi email", smtp.Message.Subject);
    }

    [Fact]
    public async Task Missing_credentials_do_not_connect()
    {
        using var smtp = new CapturingSmtp();
        var sender = new GmailIdentityEmailSender(new ConfigurationBuilder().Build(), smtp);
        var error = await Assert.ThrowsAsync<AuthFailure>(() => sender.SendVerificationAsync(Guid.NewGuid(), "customer@example.test", "private-code"));
        Assert.Equal("IDENTITY_EMAIL_UNAVAILABLE", error.Code);
        Assert.Null(smtp.Host);
    }

    [Theory]
    [InlineData("connect")]
    [InlineData("authenticate")]
    [InlineData("send")]
    public async Task Provider_failures_are_sanitized(string stage)
    {
        using var smtp = new CapturingSmtp { FailAt = stage };
        var error = await Assert.ThrowsAsync<AuthFailure>(() => Sender(smtp).SendVerificationAsync(Guid.NewGuid(), "customer@example.test", "private-code"));
        Assert.Equal(503, error.Status);
        Assert.Equal("IDENTITY_EMAIL_UNAVAILABLE", error.Code);
        Assert.DoesNotContain("sensitive-provider-response", error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task Accepted_delivery_is_successful_even_if_quit_fails()
    {
        using var smtp = new CapturingSmtp { FailAt = "disconnect" };
        await Sender(smtp).SendVerificationAsync(Guid.NewGuid(), "customer@example.test", "private-code");
        Assert.NotNull(smtp.Message);
    }

    private static GmailIdentityEmailSender Sender(ISmtpClient smtp) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentityEmail:Gmail:Address"] = "sender@gmail.com",
            ["IdentityEmail:Gmail:AppPassword"] = "abcd efgh ijkl mnop",
            ["IdentityEmail:PublicBaseUrl"] = "https://nidhi.example.test"
        }).Build(), smtp);

    private sealed class CapturingSmtp : SmtpClient
    {
        public string? Host { get; private set; }
        public int Port { get; private set; }
        public SecureSocketOptions Security { get; private set; }
        public string? Username { get; private set; }
        public string? Password { get; private set; }
        public MimeMessage? Message { get; private set; }
        public string? FailAt { get; init; }
        private void Fail(string stage) { if (FailAt == stage) throw new IOException("sensitive-provider-response"); }
        public override Task ConnectAsync(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
        {
            Fail("connect"); Host = host; Port = port; Security = options; return Task.CompletedTask;
        }
        public override Task AuthenticateAsync(Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
        {
            Fail("authenticate");
            var value = credentials.GetCredential(new Uri("smtp://smtp.gmail.com"), "PLAIN")
                ?? throw new InvalidOperationException("Missing test credentials.");
            Username = value.UserName; Password = value.Password; return Task.CompletedTask;
        }
        public override Task<string> SendAsync(FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
        {
            Fail("send"); Message = message; return Task.FromResult("accepted");
        }
        public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
        {
            Fail("disconnect"); return Task.CompletedTask;
        }
    }
}
