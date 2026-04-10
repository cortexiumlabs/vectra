namespace Vectra.BuildingBlocks.Configuration.System.Storage.Database;

public class DatabaseConfiguration
{
    public string Provider { get; set; } = "Sqlite";
    public SqliteConfiguration Sqlite { get; set; } = new();
    public PostgreSqlConfiguration PostgreSql { get; set; } = new();
}