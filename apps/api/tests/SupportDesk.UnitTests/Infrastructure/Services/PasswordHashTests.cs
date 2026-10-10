using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public sealed class PasswordHashTests
{
    [Fact]
    public void HashAndVerify_ValidPasswordSucceeds_InvalidPasswordFails()
    {
        var service = new Pbkdf2PasswordService();
        var hash = service.Hash("Correct_Password_123!");
        Assert.DoesNotContain("Correct_Password_123!", hash);
        Assert.True(service.Verify("Correct_Password_123!", hash));
        Assert.False(service.Verify("Wrong_Password_123!", hash));
        Assert.False(service.Verify("Correct_Password_123!", "invalid"));
    }

    [Fact]
    public void Hash_UsesFreshRandomSaltForEachCredential()
    {
        var service = new Pbkdf2PasswordService();
        var first = service.Hash("Same_Password_123!");
        var second = service.Hash("Same_Password_123!");
        Assert.NotEqual(first, second);
        Assert.True(service.Verify("Same_Password_123!", first));
        Assert.True(service.Verify("Same_Password_123!", second));
    }
}
