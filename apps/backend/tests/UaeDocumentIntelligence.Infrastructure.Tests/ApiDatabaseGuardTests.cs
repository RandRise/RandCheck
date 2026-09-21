using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class ApiDatabaseGuardTests
{
    [Theory]
    [InlineData("Host=127.0.0.1;Port=1;Database=test;Username=other", "enterprise_api")]
    [InlineData("Host=127.0.0.1;Port=1;Database=phase5_retained;Username=enterprise_api", "enterprise_api")]
    [InlineData("Host=127.0.0.1;Port=1;Database=test;Username=enterprise_api;Include Error Detail=true", "enterprise_api")]
    [InlineData("Host=127.0.0.1;Port=1;Database=test;Username=enterprise_api;Log Parameters=true", "enterprise_api")]
    [InlineData("Host=example.invalid;Database=test;Username=enterprise_api;SSL Mode=Require", "enterprise_api")]
    [InlineData("Host=127.0.0.1;Port=1;Database=test;Username=enterprise_api", "bad;role")]
    public async Task Invalid_connection_profiles_fail_before_connecting_without_exposing_configuration(string connection, string role)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        var guard = new ApiDatabaseGuard(db, Options.Create(new ApiDatabaseOptions { Enabled = true, DatabaseRole = role }));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => guard.VerifyAsync(default));
        Assert.Equal("The API database role, connection or grants are not configured.", error.Message); Assert.Null(error.InnerException);
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
    }
    [Fact]
    public async Task Enforced_profile_rejects_non_postgres_while_disabled_profile_leaves_development_unchanged()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        var options = new ApiDatabaseOptions { Enabled = false, DatabaseRole = "enterprise_api" };
        var guard = new ApiDatabaseGuard(db, Options.Create(options)); await guard.VerifyAsync(default);
        options.Enabled = true; await Assert.ThrowsAsync<InvalidOperationException>(() => guard.VerifyAsync(default));
    }
}
