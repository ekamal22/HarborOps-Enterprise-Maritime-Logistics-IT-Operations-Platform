using HarborOps.Api.Search;
using Xunit;

namespace HarborOps.ContractTests;

public sealed class ShipmentExportGateTests
{
    [Fact]
    public void SamePrincipalCannotHoldTwoInFlightExports()
    {
        var gate = new ShipmentExportGate();
        var principal = Guid.NewGuid();
        Assert.True(gate.TryEnter(principal));
        Assert.False(gate.TryEnter(principal));
        gate.Leave(principal);
        Assert.True(gate.TryEnter(principal));
        gate.Leave(principal);
    }

    [Fact]
    public void IndependentPrincipalsDoNotBlockOneAnother()
    {
        var gate = new ShipmentExportGate();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Assert.True(gate.TryEnter(first));
        Assert.True(gate.TryEnter(second));
        Assert.False(gate.TryEnter(first));
        Assert.False(gate.TryEnter(second));
        gate.Leave(first);
        Assert.True(gate.TryEnter(first));
        gate.Leave(first);
        gate.Leave(second);
    }
}
