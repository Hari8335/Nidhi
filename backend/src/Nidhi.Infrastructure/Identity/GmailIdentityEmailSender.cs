using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System.Net;
using Nidhi.Application.Authentication;

namespace Nidhi.Infrastructure.Identity;

public sealed class GmailIdentityEmailSender(IConfiguration configuration, ISmtpClient smtp) : IIdentityEmailSender
{
    public Task SendVerificationAsync(Guid userId, string email, string token) =>
        SendAsync(userId, email, token, "Verify your Nidhi email", "verify your email");

    public Task SendPasswordResetAsync(Guid userId, string email, string token) =>
        SendAsync(userId, email, token, "Reset your Nidhi password", "reset your password");

    private async Task SendAsync(Guid userId, string email, string token, string subject, string action)
    {
        var address = configuration["IdentityEmail:Gmail:Address"];
        var password = configuration["IdentityEmail:Gmail:AppPassword"];
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(password))
            throw Unavailable();

        try
        {
            var link = IdentityEmailLink.Create(configuration, userId, token, subject.StartsWith("Reset", StringComparison.Ordinal));
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Nidhi", MailboxAddress.Parse(address).Address));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            var body = new BodyBuilder
            {
                TextBody = $"{subject}\n\nOpen this link to {action}:\n{link}\n\nThis link expires after one hour and can only be used once. Keep it private.\nIf you did not request this, you can ignore this message.\n\nNidhi is a simulated gold savings application.",
                HtmlBody = $"<!doctype html><html><body style=\"margin:0;background:#f5f5f5;font-family:Arial,sans-serif;color:#171717\"><div style=\"max-width:520px;margin:32px auto;padding:32px;background:#fff;border-radius:12px\"><p style=\"font-weight:bold;letter-spacing:3px;color:#8a6b15\">NIDHI</p><h1 style=\"font-size:26px\">{subject}</h1><p>One small step to continue your savings journey.</p><p style=\"margin:32px 0\"><a href=\"{WebUtility.HtmlEncode(link)}\" style=\"background:#d1aa2b;color:#111;text-decoration:none;padding:16px 24px;border-radius:8px;font-weight:bold\">{(subject.StartsWith("Reset", StringComparison.Ordinal) ? "Reset password" : "Verify email")}</a></p><p>This private link expires in one hour and can only be used once.</p><p>If you did not request this, you can ignore this email.</p><hr style=\"border:0;border-top:1px solid #ddd\"><p style=\"font-size:12px;color:#666\">Simulated gold savings. No real money or gold custody.</p></div></body></html>"
            };
            message.Body = body.ToMessageBody();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            // Require TLS and normal certificate validation before sending credentials.
            await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls, timeout.Token);
            await smtp.AuthenticateAsync(address, password.Replace(" ", ""), timeout.Token);
            await smtp.SendAsync(message, timeout.Token);
            // Delivery is accepted; a QUIT failure must not roll back registration.
            try { await smtp.DisconnectAsync(true, timeout.Token); }
            catch (Exception) { }
        }
        catch (Exception)
        {
            // Provider responses can contain addresses or credentials. Never expose them.
            throw Unavailable();
        }
    }

    private static AuthFailure Unavailable() => new("IDENTITY_EMAIL_UNAVAILABLE", 503,
        "Identity email delivery is unavailable. Please retry later.");
}
