using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Nidhi.Application.Authentication;
using Nidhi.Infrastructure.Identity;
using Xunit;

namespace Nidhi.UnitTests;

public sealed class LocalIdentityEmailSenderTests
{
    [Fact]
    public async Task Development_writes_private_pickup_files_with_no_password_field()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nidhi-mail-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sender = Sender("Development", directory);
            var userId = Guid.CreateVersion7();
            var token = Guid.NewGuid().ToString("N");
            await sender.SendVerificationAsync(userId, "local@example.test", token);
            await sender.SendPasswordResetAsync(userId, "local@example.test", token);
            var files = Directory.GetFiles(directory);
            Assert.Equal(2, files.Length);
            foreach (var file in files)
            {
                var json = JsonDocument.Parse(await File.ReadAllTextAsync(file)).RootElement;
                Assert.Equal(userId, json.GetProperty("userId").GetGuid());
                Assert.Equal(token, json.GetProperty("token").GetString());
                Assert.False(json.TryGetProperty("password", out _));
                if (!OperatingSystem.IsWindows())
                    Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
            }
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Nondevelopment_never_writes_tokens(string environment)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nidhi-mail-test-" + Guid.NewGuid().ToString("N"));
        var sender = Sender(environment, directory);
        var failure = await Assert.ThrowsAsync<AuthFailure>(() => sender.SendVerificationAsync(Guid.NewGuid(), "local@example.test", Guid.NewGuid().ToString()));
        Assert.Equal("IDENTITY_EMAIL_UNAVAILABLE", failure.Code);
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("relative/path")]
    public async Task Development_requires_an_explicit_absolute_directory(string? directory)
    {
        var failure = await Assert.ThrowsAsync<AuthFailure>(() => Sender("Development", directory)
            .SendPasswordResetAsync(Guid.NewGuid(), "local@example.test", Guid.NewGuid().ToString()));
        Assert.Equal(503, failure.Status);
    }

    private static LocalIdentityEmailSender Sender(string environment, string? directory) => new(
        new TestEnvironment { EnvironmentName = environment },
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["IdentityEmail:PickupDirectory"] = directory }).Build());

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = "Nidhi.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
