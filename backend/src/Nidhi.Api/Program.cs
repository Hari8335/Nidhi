using Microsoft.AspNetCore.Antiforgery;
using Nidhi.Api.Authentication;
using Nidhi.Application.Authentication;
using Nidhi.Infrastructure;
using Nidhi.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.AddNidhiAuthentication();
builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<AuthOpenApiTransformer>());

var app = builder.Build();
if (args.Contains("provision-admin", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    try
    {
        var email = builder.Configuration["AdminProvisioning:Email"];
        var password = builder.Configuration["AdminProvisioning:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Missing protected provisioning configuration.");
        var created = await scope.ServiceProvider.GetRequiredService<AdminProvisioner>().ProvisionAsync(email, password);
        app.Logger.LogInformation("Admin provisioning completed: {Outcome}.", created ? "created" : "already provisioned");
    }
    catch (Exception)
    {
        app.Logger.LogError("Admin provisioning failed. Check protected configuration and database availability; existing customers cannot be promoted.");
        Environment.ExitCode = 1;
    }
    return;
}

// Hosting-specific trusted proxies/HTTPS redirects remain deployment configuration.
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        return Task.CompletedTask;
    });
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    try { await next(context); }
    catch (AuthFailure failure)
    {
        await Results.Problem(AuthProblems.Create(context, failure.Status, failure.Code, failure.Message, failure.Errors)).ExecuteAsync(context);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        // Navigation and deliberately lost responses can abort reads. These are not server faults.
        if (!context.Response.HasStarted) context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
    }
    catch (Exception exception)
    {
        // Intentionally avoid logging request bodies or exception objects containing provider data.
        app.Logger.LogError("API request failed ({FailureType}). Trace identifier: {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        if (context.Response.HasStarted) throw;
        context.Response.Clear();
        context.Response.Headers.CacheControl = "no-store";
        await AuthProblems.WriteAsync(context, 500, "INTERNAL_ERROR", "The request could not be completed.");
    }
});
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    if (context.Request.Path.StartsWithSegments("/api"))
        await AuthProblems.WriteAsync(context, context.Response.StatusCode,
            AuthProblems.CodeForStatus(context.Response.StatusCode), "The API request could not be completed.");
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException)
        {
            await AuthProblems.WriteAsync(context, 400, "CSRF_VALIDATION_FAILED", "Obtain a fresh antiforgery token and retry.");
            return;
        }
    }
    await next(context);
});
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.Run();

public partial class Program { }
