using DotNetEnv;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Florique.Api.Services;

/// <summary>
/// Service for accessing application configuration from environment variables and database
/// </summary>
public class ConfigurationService
{
    private static bool _isLoaded = false;
    private readonly Dictionary<string, string> _configCache = new();
    private DateTime _cacheLoadTime = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    private readonly string _encryptionKey;

    public ConfigurationService()
    {
        LoadEnvironmentVariables();
        // Use a fixed encryption key (in production, store this securely)
        _encryptionKey = GetEnvironmentVariable("CONFIG_ENCRYPTION_KEY") ?? "FLQ2025!SecureKey#ConfigEncrypt@";
    }

    private void LoadEnvironmentVariables()
    {
        if (_isLoaded) return;

        try
        {
            // Load .env file from the project root
            var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");

            if (!File.Exists(envPath))
            {
                // Try current directory
                var currentDir = Directory.GetCurrentDirectory();
                envPath = Path.Combine(currentDir, ".env");
            }

            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                _isLoaded = true;
            }
            else
            {
                // Fallback: try to load from current directory
                Env.Load();
                _isLoaded = true;
            }
        }
        catch (Exception)
        {
            // If .env file doesn't exist, environment variables should still work
            _isLoaded = true;
        }
    }

    /// <summary>
    /// Gets configuration value from database with caching, falls back to environment variables
    /// </summary>
    public async Task<string> GetConfigValueAsync(string key)
    {
        // Check if cache is still valid
        if (_configCache.ContainsKey(key) && DateTime.UtcNow - _cacheLoadTime < _cacheExpiration)
        {
            return _configCache[key];
        }

        try
        {
            // Try to get from database
            var connectionString = GetDatabaseConnectionString();
            var dbType = DbType?.ToLower() ?? "postgresql";

            if (dbType == "sqlserver")
            {
                return await GetConfigFromSqlServerAsync(key, connectionString);
            }
            else
            {
                return await GetConfigFromPostgreSqlAsync(key, connectionString);
            }
        }
        catch (Exception)
        {
            // Fall back to environment variable if database fails
            return GetEnvironmentVariable(key);
        }
    }

    private async Task<string> GetConfigFromSqlServerAsync(string key, string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = "SELECT [configValue], [isEncrypted] FROM [configurations] WHERE [configKey] = @key";
        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@key", key);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var value = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var isEncrypted = reader.GetBoolean(1);

            if (isEncrypted && !string.IsNullOrEmpty(value))
            {
                value = Decrypt(value);
            }

            // Update cache
            _configCache[key] = value;
            _cacheLoadTime = DateTime.UtcNow;

            return value;
        }

        // Not found in database, fall back to environment variable
        return GetEnvironmentVariable(key);
    }

    private async Task<string> GetConfigFromPostgreSqlAsync(string key, string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var query = "SELECT configvalue, isencrypted FROM configurations WHERE configkey = @key";
        using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@key", key);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var value = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var isEncrypted = reader.GetBoolean(1);

            if (isEncrypted && !string.IsNullOrEmpty(value))
            {
                value = Decrypt(value);
            }

            // Update cache
            _configCache[key] = value;
            _cacheLoadTime = DateTime.UtcNow;

            return value;
        }

        // Not found in database, fall back to environment variable
        return GetEnvironmentVariable(key);
    }

    /// <summary>
    /// Sets configuration value in database
    /// </summary>
    public async Task SetConfigValueAsync(string key, string value, bool isEncrypted = false)
    {
        var connectionString = GetDatabaseConnectionString();
        var dbType = DbType?.ToLower() ?? "postgresql";

        var valueToStore = isEncrypted ? Encrypt(value) : value;

        if (dbType == "sqlserver")
        {
            await SetConfigInSqlServerAsync(key, valueToStore, connectionString);
        }
        else
        {
            await SetConfigInPostgreSqlAsync(key, valueToStore, connectionString);
        }

        // Update cache
        _configCache[key] = value;
        _cacheLoadTime = DateTime.UtcNow;
    }

    private async Task SetConfigInSqlServerAsync(string key, string value, string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = "UPDATE [configurations] SET [configValue] = @value, [updatedDate] = GETUTCDATE() WHERE [configKey] = @key";
        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", (object)value ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SetConfigInPostgreSqlAsync(string key, string value, string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var query = "UPDATE configurations SET configvalue = @value, updateddate = NOW() WHERE configkey = @key";
        using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", (object)value ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Encrypts a string using AES encryption
    /// </summary>
    private string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        var key = DeriveKeyFromPassword(_encryptionKey);
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();

        // Prepend IV to the encrypted data
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Decrypts a string using AES encryption
    /// </summary>
    private string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            var key = DeriveKeyFromPassword(_encryptionKey);
            aes.Key = key;

            // Extract IV from the beginning of the cipher text
            var iv = new byte[aes.IV.Length];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }
        catch
        {
            // Return original value if decryption fails
            return cipherText;
        }
    }

    /// <summary>
    /// Derives a 256-bit key from the password
    /// </summary>
    private byte[] DeriveKeyFromPassword(string password)
    {
        var salt = Encoding.UTF8.GetBytes("FLQSalt2025"); // In production, use a unique salt per installation
        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        return deriveBytes.GetBytes(32); // 256 bits
    }

    /// <summary>
    /// Clears the configuration cache
    /// </summary>
    public void ClearCache()
    {
        _configCache.Clear();
        _cacheLoadTime = DateTime.MinValue;
    }

    // Database Configuration
    public string DbType => GetEnvironmentVariable("DB_TYPE");

    // SQL Server Configuration
    public string SqlServerServer => GetEnvironmentVariable("SQLSERVER_SERVER");
    public string SqlServerDatabase => GetEnvironmentVariable("SQLSERVER_DATABASE");
    public string SqlServerIntegratedSecurity => GetEnvironmentVariable("SQLSERVER_INTEGRATED_SECURITY");
    public string SqlServerTrustServerCertificate => GetEnvironmentVariable("SQLSERVER_TRUST_SERVER_CERTIFICATE");

    // PostgreSQL Configuration
    public string PostgreSqlHost => GetEnvironmentVariable("POSTGRESQL_HOST");
    public string PostgreSqlPort => GetEnvironmentVariable("POSTGRESQL_PORT");
    public string PostgreSqlUsername => GetEnvironmentVariable("POSTGRESQL_USERNAME");
    public string PostgreSqlPassword => GetEnvironmentVariable("POSTGRESQL_PASSWORD");
    public string PostgreSqlDatabase => GetEnvironmentVariable("POSTGRESQL_DATABASE");
    public string PostgreSqlSslMode => GetEnvironmentVariable("POSTGRESQL_SSL_MODE");
    public string PostgreSqlChannelBinding => GetEnvironmentVariable("POSTGRESQL_CHANNEL_BINDING");
    public string PostgreSqlTrustServerCertificate => GetEnvironmentVariable("POSTGRESQL_TRUST_SERVER_CERTIFICATE");

    /// <summary>
    /// Gets the database connection string based on DB_TYPE
    /// </summary>
    public string GetDatabaseConnectionString()
    {
        var dbType = DbType?.ToLower() ?? "postgresql";

        if (dbType == "sqlserver")
        {
            return GetSqlServerConnectionString();
        }
        else
        {
            return GetPostgreSqlConnectionString();
        }
    }

    /// <summary>
    /// Gets SQL Server connection string
    /// </summary>
    private string GetSqlServerConnectionString()
    {
        var builder = new System.Text.StringBuilder();
        builder.Append($"Server={SqlServerServer};");
        builder.Append($"Database={SqlServerDatabase};");

        if (SqlServerIntegratedSecurity?.ToLower() == "true")
        {
            builder.Append("Integrated Security=true;");
        }

        if (SqlServerTrustServerCertificate?.ToLower() == "true")
        {
            builder.Append("TrustServerCertificate=true;");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets PostgreSQL connection string
    /// </summary>
    private string GetPostgreSqlConnectionString()
    {
        var connectionString = $"Host={PostgreSqlHost};Port={PostgreSqlPort};Username={PostgreSqlUsername};Password={PostgreSqlPassword};Database={PostgreSqlDatabase};Ssl Mode={PostgreSqlSslMode};";

        if (!string.IsNullOrEmpty(PostgreSqlChannelBinding))
        {
            connectionString += $"Channel Binding={PostgreSqlChannelBinding};";
        }

        if (!string.IsNullOrEmpty(PostgreSqlTrustServerCertificate))
        {
            connectionString += $"Trust Server Certificate={PostgreSqlTrustServerCertificate};";
        }

        return connectionString;
    }

    private string GetEnvironmentVariable(string key)
    {
        return Environment.GetEnvironmentVariable(key) ?? string.Empty;
    }

    // Async helper methods for common configuration values
    public async Task<string> GetOpenAiApiKeyAsync() => await GetConfigValueAsync("OPENAI_API_KEY");
    public async Task<string> GetOpenAiApiEndpointAsync() => await GetConfigValueAsync("OPENAI_API_ENDPOINT");
    public async Task<string> GetOpenAiModelAsync() => await GetConfigValueAsync("OPENAI_MODEL");
    public async Task<string> GetProductId50CreditsAsync() => await GetConfigValueAsync("PRODUCT_ID_50_CREDITS");
}
