// Quick tool to check configuration in PostgreSQL
// Run with: dotnet script CheckDatabaseConfig.cs

using Npgsql;
using System;

var connString = "Host=ep-jolly-bush-a79trleo-pooler.ap-southeast-2.aws.neon.tech;Port=5432;Username=neondb_owner;Password=npg_UBgisLeoK9X2;Database=neondb;SSL Mode=VerifyFull;Trust Server Certificate=true;";

try
{
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    Console.WriteLine("=== Connected to Database ===\n");

    // Check OPENAI_MODEL
    var query = "SELECT configkey, configvalue, isencrypted, updateddate FROM configurations WHERE configkey = 'OPENAI_MODEL'";
    await using var cmd = new NpgsqlCommand(query, conn);
    await using var reader = await cmd.ExecuteReaderAsync();

    Console.WriteLine("OPENAI_MODEL Configuration:");
    Console.WriteLine("----------------------------");
    if (await reader.ReadAsync())
    {
        var key = reader.GetString(0);
        var value = reader.GetString(1);
        var isEncrypted = reader.GetBoolean(2);
        var updated = reader.GetDateTime(3);

        Console.WriteLine($"Key: {key}");
        Console.WriteLine($"Value: {value}");
        Console.WriteLine($"Is Encrypted: {isEncrypted}");
        Console.WriteLine($"Last Updated: {updated}");
    }
    else
    {
        Console.WriteLine("NOT FOUND IN DATABASE!");
    }

    await reader.CloseAsync();

    // Show all configurations
    Console.WriteLine("\n\n=== All Configurations ===\n");
    var allQuery = "SELECT configkey, configvalue, isencrypted FROM configurations ORDER BY configkey";
    await using var cmd2 = new NpgsqlCommand(allQuery, conn);
    await using var reader2 = await cmd2.ExecuteReaderAsync();

    while (await reader2.ReadAsync())
    {
        var key = reader2.GetString(0);
        var value = reader2.IsDBNull(1) ? "(null)" : reader2.GetString(1);
        var isEncrypted = reader2.GetBoolean(2);

        if (isEncrypted)
        {
            Console.WriteLine($"{key}: [ENCRYPTED]");
        }
        else
        {
            Console.WriteLine($"{key}: {value}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}
