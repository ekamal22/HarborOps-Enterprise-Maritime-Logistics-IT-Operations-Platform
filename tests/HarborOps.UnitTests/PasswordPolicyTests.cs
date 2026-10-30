using HarborOps.Infrastructure.Identity;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("short")]
    [InlineData("password123456789")]
    [InlineData("                 ")]
    public void RejectsWeakOrInvalidPasswords(string value) =>
        Assert.Throws<ArgumentException>(() => PasswordHasher.ValidateNewPassword(value));

    [Fact]
    public void NormalizesEmailAndRejectsMalformedIdentifiers()
    {
        Assert.Equal("OPERATOR@EXAMPLE.TEST", IdentityStore.NormalizeEmail(" operator@Example.Test "));
        Assert.Throws<ArgumentException>(() => IdentityStore.NormalizeEmail("bad-address"));
    }
}
