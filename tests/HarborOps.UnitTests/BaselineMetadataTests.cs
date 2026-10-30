using HarborOps.Domain;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class BaselineMetadataTests
{
    [Fact]
    public void DatasetVersion_IsExplicitlySynthetic()
    {
        Assert.Equal("m0-synthetic-v1", BaselineMetadata.DatasetVersion);
        Assert.Equal("harborops", BaselineMetadata.RecordKey);
    }
}
