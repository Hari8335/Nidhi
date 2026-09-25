using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nidhi.Application.Authentication;
using Nidhi.Infrastructure.Identity;
using Nidhi.Infrastructure.Persistence;
using Nidhi.IntegrationTests.Persistence;
using Npgsql;
using Xunit;

namespace Nidhi.IntegrationTests;

// These routes are registered only by the test host, never the production API.
[ApiController]
[Route("auth-tests")]
public sealed class AuthPolicyProbeController : ControllerBase
{
    [HttpGet("admin"), Authorize(Policy = AuthPolicies.Admin)] public IActionResult Admin() => Ok();
    [HttpGet("customer"), Authorize(Policy = AuthPolicies.Customer)] public IActionResult Customer() => Ok();
    [HttpGet("verified"), Authorize(Policy = AuthPolicies.VerifiedCustomer)] public IActionResult Verified() => Ok();
}

public sealed class TestIdentityEmails : IIdentityEmailSender
{
    public sealed record Mail(Guid UserId, string Email, string Token, string Purpose);
    public ConcurrentQueue<Mail> Messages { get; } = new();
    public bool Fail { get; set; }
    public Task SendVerificationAsync(Guid userId, string email, string token) => Send(userId, email, token, "verification");
    public Task SendPasswordResetAsync(Guid userId, string email, string token) => Send(userId, email, token, "reset");
    private Task Send(Guid userId, string email, string token, string purpose)
    {
        if (Fail) throw new AuthFailure("IDENTITY_EMAIL_UNAVAILABLE", 503, "Email delivery is unavailable.");
        Messages.Enqueue(new(userId, email, token, purpose));
        return Task.CompletedTask;
    }
}

public sealed class AuthPostgresFixture : IAsyncLifetime
{
    private NpgsqlConnection? admin;
    private string database = "";
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public TestIdentityEmails Emails { get; } = new();
    public string ConnectionString { get; private set; } = "";
    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("NIDHI_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured)) return;
        var settings = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false };
        admin = new NpgsqlConnection(settings.ConnectionString);
        await admin.OpenAsync();
        database = "nidhi_auth_test_" + Guid.NewGuid().ToString("N");
        await new NpgsqlCommand($"CREATE DATABASE {database}", admin).ExecuteNonQueryAsync();
        settings.Database = database;
        ConnectionString = settings.ConnectionString;
        Factory = NewFactory();
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NidhiDbContext>().Database.MigrateAsync();
    }

    public WebApplicationFactory<Program> NewFactory(string environment = "Development", int limit = 1000, TimeProvider? clock = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:NidhiDb"] = ConnectionString,
                ["Auth:RateLimits:register:PermitLimit"] = limit.ToString(),
                ["Auth:RateLimits:login:PermitLimit"] = limit.ToString(),
                ["Auth:RateLimits:forgot-password:PermitLimit"] = limit.ToString(),
                ["Auth:RateLimits:tokens:PermitLimit"] = limit.ToString(),
                ["Logging:LogLevel:Default"] = "Warning"
            }));
            builder.ConfigureServices(services =>
            {
                if (clock is not null)
                    services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => options.TimeProvider = clock);
                services.RemoveAll<IIdentityEmailSender>();
                services.AddSingleton<IIdentityEmailSender>(Emails);
                services.AddControllers().AddApplicationPart(typeof(AuthPolicyProbeController).Assembly);
            });
        });

    public async Task DisposeAsync()
    {
        if (admin is null) return;
        if (Factory is not null) await Factory.DisposeAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin).ExecuteNonQueryAsync();
        await admin.DisposeAsync();
    }
}

public sealed class AuthTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
{
    private static string NewEmail() => $"auth-{Guid.NewGuid():N}@example.test";
    private static string Password() => $"Aa1!{Guid.NewGuid():N}";
    private static async Task<string> Token(HttpClient client)
    {
        var result = await client.GetFromJsonAsync<AntiforgeryResponse>("/api/v1/auth/antiforgery");
        return result!.Token;
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string action, object? body, bool csrf = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/" + action);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (csrf) request.Headers.Add("X-CSRF-TOKEN", await Token(client));
        return await client.SendAsync(request);
    }
    private async Task<(string Email, string Password, Guid UserId)> Register(HttpClient client)
    {
        var email = NewEmail();
        var password = Password();
        var response = await Post(client, "register", new RegisterRequest(email, password, "Test customer"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (email, password, (await response.Content.ReadFromJsonAsync<RegistrationResponse>())!.CustomerId);
    }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("timestampUtc", out _));
        Assert.True(body.TryGetProperty("traceId", out _));
        Assert.True(body.TryGetProperty("instance", out _));
    }

    [PostgresFact]
    public async Task Registration_creates_only_customer_and_zero_balances_atomically()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await Register(client);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(account.UserId.ToString());
        Assert.Equal(new[] { "CUSTOMER" }, await users.GetRolesAsync(user!));
        Assert.False(user!.EmailConfirmed);
        Assert.NotEqual(account.Password, user.PasswordHash);
        Assert.Equal("Test customer", (await db.CustomerProfiles.FindAsync(account.UserId))!.DisplayName);
        Assert.Equal(0.00m, (await db.Wallets.SingleAsync(x => x.CustomerId == account.UserId)).BalanceLkr);
        Assert.Equal(0.00000000m, (await db.GoldHoldings.SingleAsync(x => x.CustomerId == account.UserId)).QuantityGrams);
        Assert.False(await db.SavingsGoals.AnyAsync());
        Assert.False(await db.FinancialTransactions.AnyAsync());
        Assert.False(await db.GoldPrices.AnyAsync());
        Assert.Equal(new[] { "ADMIN", "CUSTOMER" }, await db.Roles.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
        await Error(await Post(client, "register", new RegisterRequest(account.Email.ToUpperInvariant(), Password())), HttpStatusCode.Conflict, "EMAIL_ALREADY_EXISTS");
        var forbiddenEmail = NewEmail();
        await Error(await Post(client, "register", new { email = forbiddenEmail, password = Password(), role = "ADMIN" }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Null(await users.FindByEmailAsync(forbiddenEmail));
    }

    [PostgresFact]
    public async Task Registration_delivery_failure_rolls_back_and_can_be_retried()
    {
        using var client = fixture.Factory.CreateClient();
        var email = NewEmail();
        var request = new RegisterRequest(email, Password());
        fixture.Emails.Fail = true;
        try { await Error(await Post(client, "register", request), HttpStatusCode.ServiceUnavailable, "IDENTITY_EMAIL_UNAVAILABLE"); }
        finally { fixture.Emails.Fail = false; }
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email));
        Assert.Equal(HttpStatusCode.Created, (await Post(client, "register", request)).StatusCode);
    }

    [PostgresFact]
    public async Task Full_auth_flow_verifies_logs_in_revokes_logout_and_resets_password()
    {
        using var client = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        var account = await Register(client);
        var verification = fixture.Emails.Messages.Last(x => x.Email == account.Email && x.Purpose == "verification");
        await Error(await Post(client, "verify-email", new VerifyEmailRequest(account.UserId, "invalid")), HttpStatusCode.BadRequest, "INVALID_TOKEN");
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "verify-email", new VerifyEmailRequest(account.UserId, verification.Token))).StatusCode);
        await Error(await Post(client, "verify-email", new VerifyEmailRequest(account.UserId, verification.Token)), HttpStatusCode.BadRequest, "INVALID_TOKEN");
        await Error(await Post(client, "login", new LoginRequest(account.Email, Password())), HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
        var login = await Post(client, "login", new LoginRequest(account.Email, account.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(".Nidhi.Session="));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.DoesNotContain("expires=", cookie.ToLowerInvariant());
        Assert.DoesNotContain("max-age=", cookie.ToLowerInvariant());
        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var dto = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "displayName", "email", "isEmailVerified", "roles", "userId" }, dto.EnumerateObject().Select(x => x.Name).Order().ToArray());
        Assert.True(dto.GetProperty("isEmailVerified").GetBoolean());
        Assert.Equal(account.UserId, dto.GetProperty("userId").GetGuid());
        await Error(await Post(client, "logout", null, csrf: false), HttpStatusCode.BadRequest, "CSRF_VALIDATION_FAILED");
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "logout", null)).StatusCode);
        await Error(await client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED");
        using var replay = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        await Error(await replay.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED");
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "forgot-password", new ForgotPasswordRequest(account.Email))).StatusCode);
        var reset = fixture.Emails.Messages.Last(x => x.Email == account.Email && x.Purpose == "reset");
        var newPassword = Password();
        await Error(await Post(client, "reset-password", new ResetPasswordRequest(account.UserId, "invalid", newPassword)), HttpStatusCode.BadRequest, "INVALID_TOKEN");
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "reset-password", new ResetPasswordRequest(account.UserId, reset.Token, newPassword))).StatusCode);
        await Error(await Post(client, "reset-password", new ResetPasswordRequest(account.UserId, reset.Token, Password())), HttpStatusCode.BadRequest, "INVALID_TOKEN");
        await Error(await Post(client, "login", new LoginRequest(account.Email, account.Password)), HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "login", new LoginRequest(account.Email, newPassword))).StatusCode);
    }

    [PostgresFact]
    public async Task Policies_check_roles_and_live_email_state_and_admin_provisioning_is_idempotent()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await Register(client);
        await Post(client, "login", new LoginRequest(account.Email, account.Password));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth-tests/customer")).StatusCode);
        await Error(await client.GetAsync("/auth-tests/admin"), HttpStatusCode.Forbidden, "FORBIDDEN");
        await Error(await client.GetAsync("/auth-tests/verified"), HttpStatusCode.Forbidden, "EMAIL_NOT_VERIFIED");
        var verification = fixture.Emails.Messages.Last(x => x.Email == account.Email && x.Purpose == "verification");
        await Post(client, "verify-email", new VerifyEmailRequest(account.UserId, verification.Token));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth-tests/verified")).StatusCode);
        var adminEmail = NewEmail();
        var adminPassword = Password();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var provisioner = scope.ServiceProvider.GetRequiredService<AdminProvisioner>();
            Assert.True(await provisioner.ProvisionAsync(adminEmail, adminPassword));
            Assert.False(await provisioner.ProvisionAsync(adminEmail, Password()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => provisioner.ProvisionAsync(account.Email, Password()));
        }
        using var adminClient = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await Post(adminClient, "login", new LoginRequest(adminEmail, adminPassword))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/auth-tests/admin")).StatusCode);
        await Error(await adminClient.GetAsync("/auth-tests/customer"), HttpStatusCode.Forbidden, "FORBIDDEN");
        using var dbScope = fixture.Factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == adminEmail);
        Assert.Single(await db.AuditEvents.Where(x => x.EntityId == user.Id.ToString() && x.Action == "ADMIN_PROVISIONED").ToListAsync());
        Assert.Null(await db.CustomerProfiles.FindAsync(user.Id));
    }

    [PostgresFact]
    public async Task Recovery_is_non_enumerating_even_when_delivery_fails()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await Register(client);
        var known = await Post(client, "forgot-password", new ForgotPasswordRequest(account.Email));
        var unknown = await Post(client, "forgot-password", new ForgotPasswordRequest(NewEmail()));
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        fixture.Emails.Fail = true;
        try
        {
            var failed = await Post(client, "forgot-password", new ForgotPasswordRequest(account.Email));
            Assert.Equal(known.StatusCode, failed.StatusCode);
            Assert.Equal(await known.Content.ReadAsStringAsync(), await failed.Content.ReadAsStringAsync());
        }
        finally { fixture.Emails.Fail = false; }
    }

    [PostgresFact]
    public async Task Csrf_protects_anonymous_posts_and_refreshes_after_identity_changes()
    {
        using var client = fixture.Factory.CreateClient();
        await Error(await Post(client, "register", new RegisterRequest(NewEmail(), Password()), false), HttpStatusCode.BadRequest, "CSRF_VALIDATION_FAILED");
        var account = await Register(client);
        var anonymousToken = await Token(client);
        await Post(client, "login", new LoginRequest(account.Email, account.Password));
        using var stale = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        stale.Headers.Add("X-CSRF-TOKEN", anonymousToken);
        await Error(await client.SendAsync(stale), HttpStatusCode.BadRequest, "CSRF_VALIDATION_FAILED");
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "logout", null)).StatusCode);
    }

    [PostgresFact]
    public async Task Rate_limiting_returns_problem_details_and_retry_after()
    {
        using var factory = fixture.NewFactory(limit: 1);
        using var client = factory.CreateClient();
        foreach (var (action, body) in new (string, object)[]
        {
            ("register", new RegisterRequest(NewEmail(), Password())),
            ("login", new LoginRequest(NewEmail(), Password())),
            ("forgot-password", new ForgotPasswordRequest(NewEmail()))
        })
        {
            await Post(client, action, body);
            var response = await Post(client, action, body);
            await Error(response, HttpStatusCode.TooManyRequests, "RATE_LIMITED");
            Assert.NotNull(response.Headers.RetryAfter);
        }
    }

    [PostgresFact]
    public async Task Production_cookies_are_secure_and_openapi_describes_auth()
    {
        using var factory = fixture.NewFactory("Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var account = await Register(client);
        var response = await Post(client, "login", new LoginRequest(account.Email, account.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("secure", response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(".Nidhi.Session=")).ToLowerInvariant());
        var csrf = await client.GetAsync("/api/v1/auth/antiforgery");
        Assert.Contains("secure", csrf.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(".Nidhi.Antiforgery=")).ToLowerInvariant());
        using var development = fixture.Factory.CreateClient();
        var json = await development.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var paths = json.GetProperty("paths");
        Assert.Equal(8, paths.EnumerateObject().Count(x => x.Name.StartsWith("/api/v1/auth/")));
        Assert.Equal("cookie", json.GetProperty("components").GetProperty("securitySchemes").GetProperty("IdentityCookie").GetProperty("in").GetString());
        Assert.Contains(paths.GetProperty("/api/v1/auth/login").GetProperty("post").GetProperty("parameters").EnumerateArray(), x => x.GetProperty("name").GetString() == "X-CSRF-TOKEN" && x.GetProperty("required").GetBoolean());
        Assert.True(paths.GetProperty("/api/v1/auth/me").GetProperty("get").TryGetProperty("security", out _));
    }

    [PostgresFact]
    public async Task Tokens_are_bound_to_account_and_expire_and_reset_revokes_existing_sessions()
    {
        using var client = fixture.Factory.CreateClient();
        var first = await Register(client);
        var second = await Register(client);
        var verification = fixture.Emails.Messages.Last(x => x.Email == first.Email && x.Purpose == "verification");
        await Error(await Post(client, "verify-email", new VerifyEmailRequest(second.UserId, verification.Token)), HttpStatusCode.BadRequest, "INVALID_TOKEN");
        await Post(client, "login", new LoginRequest(first.Email, first.Password));
        await Post(client, "forgot-password", new ForgotPasswordRequest(first.Email));
        var reset = fixture.Emails.Messages.Last(x => x.Email == first.Email && x.Purpose == "reset");
        await Error(await Post(client, "reset-password", new ResetPasswordRequest(second.UserId, reset.Token, Password())), HttpStatusCode.BadRequest, "INVALID_TOKEN");
        // Use the real Identity provider with an already-expired lifespan, no token forgery.
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(first.UserId.ToString()))!;
            var provider = new DataProtectorTokenProvider<ApplicationUser>(
                scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(),
                Microsoft.Extensions.Options.Options.Create(new DataProtectionTokenProviderOptions { TokenLifespan = TimeSpan.FromSeconds(-1) }),
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DataProtectorTokenProvider<ApplicationUser>>>());
            Assert.False(await provider.ValidateAsync("EmailConfirmation", verification.Token, users, user));
            Assert.False(await provider.ValidateAsync("ResetPassword", reset.Token, users, user));
        }
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "reset-password", new ResetPasswordRequest(first.UserId, reset.Token, Password()))).StatusCode);
        await Error(await client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED");
        using var dbScope = fixture.Factory.Services.CreateScope();
        Assert.False((await dbScope.ServiceProvider.GetRequiredService<NidhiDbContext>().Users.FindAsync(first.UserId))!.EmailConfirmed);
    }
    [PostgresFact]
    public async Task Concurrent_registration_cannot_duplicate_normalized_email()
    {
        using var first = fixture.Factory.CreateClient();
        using var second = fixture.Factory.CreateClient();
        var email = NewEmail();
        var password = Password();
        var responses = await Task.WhenAll(
            Post(first, "register", new RegisterRequest(email, password)),
            Post(second, "register", new RegisterRequest(email.ToUpperInvariant(), password)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var user = await db.Users.SingleAsync(x => x.NormalizedEmail == email.ToUpperInvariant());
        Assert.Single(await db.Wallets.Where(x => x.CustomerId == user.Id).ToListAsync());
        Assert.Single(await db.GoldHoldings.Where(x => x.CustomerId == user.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task Sessions_expire_after_eight_hours_and_failed_logins_lock_out()
    {
        var clock = new TestClock();
        using var factory = fixture.NewFactory(clock: clock);
        using var client = factory.CreateClient();
        var account = await Register(client);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "login", new LoginRequest(account.Email, account.Password))).StatusCode);
        clock.Advance(TimeSpan.FromHours(7));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        clock.Advance(TimeSpan.FromHours(2));
        await Error(await client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED");
        for (var i = 0; i < 5; i++)
            await Error(await Post(client, "login", new LoginRequest(account.Email, Password())), HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
        await Error(await Post(client, "login", new LoginRequest(account.Email, account.Password)), HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
    }

    [PostgresFact]
    public async Task Framework_rejections_also_use_the_standard_problem_envelope()
    {
        using var client = fixture.Factory.CreateClient();
        using var unsupported = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        { Content = new StringContent("not-json") };
        unsupported.Headers.Add("X-CSRF-TOKEN", await Token(client));
        await Error(await client.SendAsync(unsupported), HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
        await Error(await client.GetAsync("/api/v1/auth/unknown"), HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
        await Error(await client.GetAsync("/api/v1/auth/login"), HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED");
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }

}
