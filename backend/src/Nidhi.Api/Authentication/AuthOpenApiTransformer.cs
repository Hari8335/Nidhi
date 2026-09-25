using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Nidhi.Api.Authentication;

public sealed class AuthOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["IdentityCookie"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = ".Nidhi.Session",
            Description = "ASP.NET Core Identity HttpOnly, SameSite=Strict session cookie; Secure outside localhost Development. No remember-me. Fixed 8-hour ticket."
        };
        foreach (var (path, item) in document.Paths.Where(x => x.Key.StartsWith("/api/v1/auth/", StringComparison.Ordinal)))
        {
            foreach (var (method, operation) in item.Operations!)
            {
                operation.Description = "Responses must not be cached. Obtain CSRF tokens through GET /api/v1/auth/antiforgery; keep tokens in memory only. Refresh after login, logout, reset, or session expiry.";
                if (method == HttpMethod.Post)
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = "X-CSRF-TOKEN", In = ParameterLocation.Header, Required = true,
                        Description = "ASP.NET Core antiforgery request token, paired with the .Nidhi.Antiforgery HttpOnly cookie. Required even for anonymous POSTs.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    });
                }
                if (path.EndsWith("/me", StringComparison.Ordinal) || path.EndsWith("/logout", StringComparison.Ordinal))
                    operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("IdentityCookie", document)] = [] }];
            }
        }
        return Task.CompletedTask;
    }
}
