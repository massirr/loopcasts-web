using LoopCastsWeb.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoopCastsWeb.Tests;

/// A real SQLite database that lives in memory: same SQL and UNIQUE indexes as loopcasts.db, gone after the test.
public static class TestDb
{
    public static AppDbContext Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();   // the in-memory db exists as long as this connection is open
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
