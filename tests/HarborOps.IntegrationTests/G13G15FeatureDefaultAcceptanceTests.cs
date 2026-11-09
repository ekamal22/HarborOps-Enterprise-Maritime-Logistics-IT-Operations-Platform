using Microsoft.Extensions.Configuration;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class G13G15FeatureDefaultAcceptanceTests
{
    [Fact]
    public void M10ProjectorAndM11EvaluatorRequireExplicitOptIn()
    {
        // Read the actual packaged default configuration, not an override
        // supplied by a disposable verification host.
        var path = M10RuntimeIdentityTests.RepositoryFile(
            "src/HarborOps.Api/appsettings.json");
        var config = new ConfigurationBuilder().AddJsonFile(path).Build();
        Assert.False(config.GetValue<bool>("M10:EnableProjector"));
        Assert.False(config.GetValue<bool>("M11:EnableEvaluator"));
        var program = File.ReadAllText(M10RuntimeIdentityTests.RepositoryFile(
            "src/HarborOps.Api/Program.cs"));
        Assert.Contains("builder.Configuration.GetValue<bool>(\"M10:EnableProjector\")", program);
        Assert.Contains("builder.Configuration.GetValue<bool>(\"M11:EnableEvaluator\")", program);
    }
}
