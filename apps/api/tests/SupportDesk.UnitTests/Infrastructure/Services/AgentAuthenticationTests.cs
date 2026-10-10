using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public sealed class AgentAuthenticationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly AgentAuthenticationService _auth;

    public AgentAuthenticationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        var passwords = new Pbkdf2PasswordService();
        var active = new Agent("Active Agent", "active@example.com", 5, FixedClock.DefaultNow);
        var inactive = new Agent("Inactive Agent", "inactive@example.com", 5, FixedClock.DefaultNow);
        inactive.Deactivate();
        _db.AddRange(active, inactive);
        _db.SaveChanges();
        _db.AddRange(
            new AgentCredential(active.Id, passwords.Hash("CorrectPassword1!")),
            new AgentCredential(inactive.Id, passwords.Hash("CorrectPassword1!")));
        _db.SaveChanges();
        _auth = new AgentAuthenticationService(_db, passwords);
    }

    [Fact]
    public async Task CorrectCredentials_AllowActiveAgent()
    {
        var result = await _auth.AuthenticateAsync("active@example.com", "CorrectPassword1!", default);
        Assert.NotNull(result);
        Assert.Equal("Active Agent", result.FullName);
    }

    [Theory]
    [InlineData("active@example.com", "WrongPassword")]
    [InlineData("missing@example.com", "CorrectPassword1!")]
    [InlineData("inactive@example.com", "CorrectPassword1!")]
    public async Task InvalidOrInactiveCredentials_AreRejected(string email, string password)
    {
        Assert.Null(await _auth.AuthenticateAsync(email, password, default));
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }
}
