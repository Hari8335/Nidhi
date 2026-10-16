using Microsoft.Extensions.Configuration;

namespace Nidhi.Infrastructure.Identity;

public static class IdentityEmailLink
{
    public static string Create(IConfiguration configuration, Guid userId, string token, bool reset = false)
    {
        if (!Uri.TryCreate(configuration["IdentityEmail:PublicBaseUrl"], UriKind.Absolute, out var origin)
            || (origin.Scheme != "https" && !(origin.Scheme == "http" && origin.IsLoopback))
            || !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
            throw new InvalidOperationException("Configure IdentityEmail:PublicBaseUrl with the trusted frontend origin (HTTPS, or localhost HTTP for development).");
        // Fragments are not sent in HTTP requests or included in server access logs.
        return $"{origin.GetLeftPart(UriPartial.Authority)}/{(reset ? "reset-password" : "verify-email")}#userId={userId}&token={Uri.EscapeDataString(token)}";
    }
}
