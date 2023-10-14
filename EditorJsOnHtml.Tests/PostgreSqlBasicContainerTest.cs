using Dapper;
using Npgsql;
using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        await _postgre_sql_container.StopAsync();
        await _postgre_sql_container.DisposeAsync();
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        await _postgre_sql_container.StartAsync();

        // Establish a connection to the PostgreSQL container
        string connection_string = _postgre_sql_container.GetConnectionString();
        using NpgsqlConnection connection = new(connection_string);
        await connection.OpenAsync();

        // Create the table with a UUID/GUID identifier and a JSONB column
        using NpgsqlCommand createTableCommand = new("CREATE TABLE test_table (id UUID DEFAULT gen_random_uuid(), data JSONB)", connection);
        await createTableCommand.ExecuteNonQueryAsync();

        // Insert two rows of data with JSON
        using NpgsqlCommand insert_data_command = new("INSERT INTO test_table (data) VALUES (@data)", connection);

        NpgsqlParameter npgsql_parameter = new("@data", NpgsqlTypes.NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Deserialize<object>(JsonBlob.Data)
        };
        insert_data_command.Parameters.Add(npgsql_parameter);
        await insert_data_command.ExecuteNonQueryAsync();

        insert_data_command.Parameters["@data"].Value = JsonSerializer.Deserialize<object>("{ \"key\": \"value\" }");
        await insert_data_command.ExecuteNonQueryAsync();

        // Close the connection
        connection.Close();
    }

    [TestMethod]
    public async Task ExecuteBasicCommandAsync()
    {
        // Arrange
        string connection_string = _postgre_sql_container.GetConnectionString();
        using NpgsqlConnection connection = new(connection_string);
        using NpgsqlCommand command = new();

        await connection.OpenAsync();
        command.Connection = connection;
        command.CommandText = "SELECT 1";

        // Act
        NpgsqlDataReader npgsql_data_reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleResult);
        bool npgsql_read = await npgsql_data_reader.ReadAsync();

        // Assert
        Assert.IsTrue(npgsql_read);

        int result = npgsql_data_reader.GetInt32(0);
        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public async Task ExecuteSelectCommandAsync()
    {
        // Arrange
        string connection_string = _postgre_sql_container.GetConnectionString();
        using NpgsqlConnection connection = new(connection_string);
        using NpgsqlCommand command = new();

        await connection.OpenAsync();
        command.Connection = connection;
        command.CommandText = "SELECT data FROM test_table LIMIT 1";

        // Act
        using NpgsqlDataReader npgsql_data_reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleResult);
        bool npgsql_read = await npgsql_data_reader.ReadAsync();

        // Assert
        Assert.IsTrue(npgsql_read);

        // note (2023-10-14|kibble): The JsonObject contains unicode escape sequences instead of just UTF-8 encoded strings
        JsonObject json_object = await npgsql_data_reader.GetFieldValueAsync<JsonObject>(0);
        Assert.IsNotNull(json_object);

        // Close the connection
        connection.Close();
        connection.Dispose();
    }

    [TestMethod]
    public async Task ExecuteDapperSelectCommandAsync()
    {
        // Arrange
        string connection_string = _postgre_sql_container.GetConnectionString();
        using NpgsqlConnection connection = new(connection_string);
        await connection.OpenAsync();

        SqlMapper.AddTypeHandler(new JsonObjectTypeHandler());

        // Act
        JsonObject? result = await connection.QueryFirstOrDefaultAsync<JsonObject?>("SELECT data FROM test_table LIMIT 1");

        // Assert
        Assert.IsNotNull(result);

        // Close the connection
        connection.Close(); ;
        connection.Dispose();
    }
}

public class JsonObjectTypeHandler : SqlMapper.TypeHandler<JsonObject?>
{
    public override JsonObject? Parse(object value)
        => value is string jsonString ? JsonNode.Parse(jsonString) as JsonObject : null;

    public override void SetValue(IDbDataParameter parameter, JsonObject? value)
        => parameter.Value = value?.ToString();
}
