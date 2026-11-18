using System.Text.Json;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M18BrowserSeedFactAttribute : FactAttribute
{
    public M18BrowserSeedFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HARBOROPS_G18_BROWSER_FIXTURE")))
            Skip = "Browser fixture seeding runs only when a private fixture path is provided";
    }
}

// Not a verification witness. It prepares the disposable two-organization, five-role dataset that
// the three-browser campaign signs in to, and writes the private fixture description (including
// the generated disposable role secrets) to a path on the verification drive, never into the repository.
[Collection("M12ServerRoleIsolation")]
public sealed class M18BrowserSeedTests
{
    [M18BrowserSeedFact]
    public async Task WriteDisposableBrowserFixture()
    {
        var path = Environment.GetEnvironmentVariable("HARBOROPS_G18_BROWSER_FIXTURE")!;
        Assert.StartsWith("E:", path, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(path), "Never overwrite an existing fixture");
        using var world = await M18World.CreateAsync();
        var fixture = new
        {
            password = M18World.Password,
            apiConnection = world.Roles.Api,
            adminConnection = world.Roles.Admin,
            correctionKey = ReferenceDataHttpTests.CorrectionProofKey,
            exportKey = ReferenceDataHttpTests.ExportProofKey,
            organizations = new { alpha = world.A, beta = world.B, alphaName = "M18 Alpha", betaName = "M18 Beta" },
            users = world.Users.ToDictionary(x => x.Key, x => new { email = x.Value.Email, id = x.Value.Id })
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(fixture, new JsonSerializerOptions { WriteIndented = true }));
    }
}
