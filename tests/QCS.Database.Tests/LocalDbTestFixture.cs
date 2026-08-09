using Microsoft.EntityFrameworkCore;
using QCS.Infrastructure.Data;

namespace QCS.Database.Tests;

public class LocalDbTestFixture : IDisposable
{
    public string DatabaseName { get; }
    public string ConnectionString { get; }

    public LocalDbTestFixture()
    {
        DatabaseName = $"QCS_Test_{Guid.NewGuid():N}";
        ConnectionString = $"Server=(localdb)\\mssqllocaldb;Database={DatabaseName};Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=15;";

        try
        {
            using var db = CreateDbContext();
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to connect to or migrate LocalDB instance for database '{DatabaseName}'. " +
                $"Prerequisite: LocalDB must be installed and running (sqllocaldb info MSSQLLocalDB). " +
                $"Connection string used: '{ConnectionString}'. Inner error: {ex.Message}", ex);
        }
    }

    public AppDbContext CreateDbContext()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", ConnectionString);
        return new AppDbContextDesignTimeFactory().CreateDbContext(Array.Empty<string>());
    }

    public void Dispose()
    {
        try
        {
            using var db = CreateDbContext();
            db.Database.EnsureDeleted();
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
