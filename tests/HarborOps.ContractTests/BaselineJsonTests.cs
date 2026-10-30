using System.Text.Json;
using HarborOps.Application.Baseline;
using Xunit;

namespace HarborOps.ContractTests;

public sealed class BaselineJsonTests
{
    [Fact]
    public void BaselineResponse_UsesWebJsonNames()
    {
        var dto = new BaselineStatus(
            "HarborOps", "m0-synthetic-v1",
            new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero), "synthetic");

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"datasetVersion\"", json, StringComparison.Ordinal);
        Assert.Contains("\"seededAtUtc\"", json, StringComparison.Ordinal);
        Assert.Contains("\"synthetic\"", json, StringComparison.Ordinal);
    }
}
