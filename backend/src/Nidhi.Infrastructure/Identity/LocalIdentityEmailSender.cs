using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Nidhi.Application.Authentication;

namespace Nidhi.Infrastructure.Identity;

// Development-only private pickup files; never log identity tokens or expose them over HTTP.
public sealed class LocalIdentityEmailSender(IHostEnvironment environment, IConfiguration configuration) : IIdentityEmailSender
{
    public Task SendVerificationAsync(Guid userId, string email, string token) => WriteAsync("verification", userId, email, token);
    public Task SendPasswordResetAsync(Guid userId, string email, string token) => WriteAsync("password-reset", userId, email, token);

    private async Task WriteAsync(string purpose, Guid userId, string email, string token)
    {
        if (!environment.IsDevelopment())
            throw new AuthFailure("IDENTITY_EMAIL_UNAVAILABLE", 503, "Identity email delivery is unavailable. Please retry later.");
        var directory = configuration["IdentityEmail:PickupDirectory"];
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw new AuthFailure("IDENTITY_EMAIL_UNAVAILABLE", 503, "Configure a private absolute identity email pickup directory.");
        try
        {
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var path = Path.Combine(directory, $"{Guid.NewGuid():N}.json");
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using var stream = new FileStream(path, options);
            await JsonSerializer.SerializeAsync(stream, new { purpose, userId, email, token });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AuthFailure("IDENTITY_EMAIL_UNAVAILABLE", 503, "Identity email delivery is unavailable. Please retry later.");
        }
    }
}
