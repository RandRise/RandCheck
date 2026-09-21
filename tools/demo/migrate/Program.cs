using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Infrastructure.Persistence;

// Deliberately fixed: this disposable-only tool cannot target an operator-supplied database.
const string connection = "Host=db;Port=5432;Database=document_intelligence_demo;Username=demo;Password=disposable_demo_only";
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connection).Options);
await db.Database.MigrateAsync();
Console.WriteLine($"Demo schema ready: {(await db.Database.GetAppliedMigrationsAsync()).Count()} migrations.");
