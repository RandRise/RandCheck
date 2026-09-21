using Microsoft.EntityFrameworkCore;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public void Model_matches_the_latest_migration_snapshot()
    {
        using var context = _database.CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
