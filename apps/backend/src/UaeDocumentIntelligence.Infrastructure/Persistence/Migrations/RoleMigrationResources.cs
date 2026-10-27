namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations;

/// <summary>Shared inventory SQL binds a reviewed plan to the exact migration inputs and outputs.</summary>
public static class RoleMigrationResources
{
    public static string InventorySql
    {
        get
        {
            using var stream = typeof(RoleMigrationResources).Assembly.GetManifestResourceStream(
                "UaeDocumentIntelligence.Infrastructure.Persistence.Migrations.Sql.StaffAdminInventory.sql")
                ?? throw new InvalidOperationException("Role migration inventory resource is missing.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Trim().TrimEnd(';');
        }
    }
}
