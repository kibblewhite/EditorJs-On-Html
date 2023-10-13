using Npgsql;
using Testcontainers.PostgreSql;

namespace EditorJsOnHtml.Tests;

/// <summary>
/// The purpose of this unit test is to check that an unpopulated postgresql container starts without issue and a basic integer value is returned.
/// </summary>
[TestClass]
public class PostgreSqlBasicContainerTest
{
    private readonly PostgreSqlContainer _postgre_sql_container = new PostgreSqlBuilder()
        .WithImage("postgres:16.0-alpine")
        .WithDatabase("db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithCleanUp(true)
        .Build();

    [TestCleanup]
    public async Task TestCleanup()
    {
        await _postgre_sql_container.StopAsync().ConfigureAwait(false);
        await _postgre_sql_container.DisposeAsync().ConfigureAwait(false);
    }

    [TestInitialize]
    public async Task TestInitialize()
        => await _postgre_sql_container.StartAsync().ConfigureAwait(false);

    [TestMethod]
    public async Task ExecuteCommandAsync()
    {
        // Arrange
        string connection_string = _postgre_sql_container.GetConnectionString();
        using NpgsqlConnection connection = new(connection_string);
        using NpgsqlCommand command = new();

        connection.Open();
        command.Connection = connection;
        command.CommandText = "SELECT 1";

        // Act
        NpgsqlDataReader npgsql_data_reader = await command.ExecuteReaderAsync();

        // Assert
        Assert.IsTrue(npgsql_data_reader.Read());

        int result = npgsql_data_reader.GetInt32(0);
        Assert.AreEqual(1, result);
    }
}
