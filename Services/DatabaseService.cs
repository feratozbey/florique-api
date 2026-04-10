using Npgsql;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace Florique.Api.Services;

/// <summary>
/// Service for handling all database operations
/// </summary>
public class DatabaseService
{
    private readonly ConfigurationService _configService;
    private readonly bool _isSqlServer;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(ConfigurationService configService, ILogger<DatabaseService> logger)
    {
        _configService = configService;
        _logger = logger;
        _isSqlServer = _configService.DbType?.ToLower() == "sqlserver";
    }

    /// <summary>
    /// Creates a database connection based on the configured database type
    /// </summary>
    private DbConnection CreateConnection()
    {
        var connString = _configService.GetDatabaseConnectionString();

        if (_isSqlServer)
        {
            return new SqlConnection(connString);
        }
        else
        {
            return new NpgsqlConnection(connString);
        }
    }

    /// <summary>
    /// Loads background options from the database (names only, for backwards compatibility)
    /// </summary>
    public async Task<List<string>> LoadBackgroundOptionsAsync()
    {
        var backgrounds = new List<string>();

        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            // SQL Server uses [] for identifiers, PostgreSQL uses lowercase column names
            var cmdText = _isSqlServer
                ? "SELECT [background] FROM [backgrounds] ORDER BY [id];"
                : @"SELECT background FROM backgrounds ORDER BY id;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                backgrounds.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in LoadBackgroundOptionsAsync");
            // Return fallback backgrounds if DB fails
            return new List<string> { "soft grey", "white", "studio" };
        }

        return backgrounds.Count > 0 ? backgrounds : new List<string> { "soft grey", "white", "studio" };
    }

    /// <summary>
    /// Loads background options with descriptions from the database
    /// </summary>
    public async Task<List<BackgroundOption>> LoadBackgroundOptionsWithDescriptionsAsync()
    {
        var backgrounds = new List<BackgroundOption>();

        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "SELECT [background], [bgdescription] FROM [backgrounds] ORDER BY [id];"
                : @"SELECT background, bgdescription FROM backgrounds ORDER BY id;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                backgrounds.Add(new BackgroundOption
                {
                    Background = reader.GetString(0),
                    Description = reader.IsDBNull(1) ? null : reader.GetString(1)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in LoadBackgroundOptionsWithDescriptionsAsync");
        }

        return backgrounds;
    }

    /// <summary>
    /// Background option with description
    /// </summary>
    public class BackgroundOption
    {
        public string Background { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Registers a new user in the database if they don't already exist, or updates existing user's NULL fields
    /// </summary>
    public async Task<bool> RegisterUserAsync(string userId, string? deviceType = null, string? ipAddress = null, string? location = null)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string cmdText;
            if (_isSqlServer)
            {
                // SQL Server: Insert new user or update existing user's NULL fields
                cmdText = @"
                    IF EXISTS (SELECT 1 FROM [users] WHERE [userId] = @userId)
                    BEGIN
                        -- Update existing user's NULL fields only
                        UPDATE [users]
                        SET [createdDate] = COALESCE([createdDate], @createdDate),
                            [deviceType] = COALESCE([deviceType], @deviceType),
                            [ipAddress] = COALESCE([ipAddress], @ipAddress),
                            [location] = COALESCE([location], @location)
                        WHERE [userId] = @userId
                    END
                    ELSE
                    BEGIN
                        -- Insert new user
                        INSERT INTO [users] ([userId], [createdDate], [deviceType], [ipAddress], [location])
                        VALUES (@userId, @createdDate, @deviceType, @ipAddress, @location)
                    END";
            }
            else
            {
                // PostgreSQL: Insert or update on conflict
                cmdText = @"INSERT INTO users (userid, createddate, devicetype, ipaddress, location)
                    VALUES (@userId, @createdDate, @deviceType, @ipAddress, @location)
                    ON CONFLICT (userid) DO UPDATE SET
                        createddate = COALESCE(users.createddate, EXCLUDED.createddate),
                        devicetype = COALESCE(users.devicetype, EXCLUDED.devicetype),
                        ipaddress = COALESCE(users.ipaddress, EXCLUDED.ipaddress),
                        location = COALESCE(users.location, EXCLUDED.location);";
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            var paramUserId = cmd.CreateParameter();
            paramUserId.ParameterName = "@userId";
            paramUserId.Value = userId;
            cmd.Parameters.Add(paramUserId);

            var paramCreatedDate = cmd.CreateParameter();
            paramCreatedDate.ParameterName = "@createdDate";
            paramCreatedDate.Value = DateTime.UtcNow;
            cmd.Parameters.Add(paramCreatedDate);

            var paramDeviceType = cmd.CreateParameter();
            paramDeviceType.ParameterName = "@deviceType";
            paramDeviceType.Value = (object?)deviceType ?? DBNull.Value;
            cmd.Parameters.Add(paramDeviceType);

            var paramIpAddress = cmd.CreateParameter();
            paramIpAddress.ParameterName = "@ipAddress";
            paramIpAddress.Value = (object?)ipAddress ?? DBNull.Value;
            cmd.Parameters.Add(paramIpAddress);

            var paramLocation = cmd.CreateParameter();
            paramLocation.ParameterName = "@location";
            paramLocation.Value = (object?)location ?? DBNull.Value;
            cmd.Parameters.Add(paramLocation);

            await cmd.ExecuteNonQueryAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in RegisterUserAsync for userId: {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Gets the credit balance for a user
    /// </summary>
    public async Task<int?> GetCreditsAsync(string userId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "SELECT [credit] FROM [users] WHERE [userId] = @userId;"
                : @"SELECT credit FROM users WHERE userid = @userId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            var param = cmd.CreateParameter();
            param.ParameterName = "@userId";
            param.Value = userId;
            cmd.Parameters.Add(param);

            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value && int.TryParse(result.ToString(), out int credit))
                return credit;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetCreditsAsync for userId: {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Gets complete user information including credits, creation date, device type, IP address, and location
    /// </summary>
    public async Task<UserInfo?> GetUserAsync(string userId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "SELECT [credit], [createdDate], [deviceType], [ipAddress], [location] FROM [users] WHERE [userId] = @userId;"
                : @"SELECT credit, createddate, devicetype, ipaddress, location FROM users WHERE userid = @userId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            var param = cmd.CreateParameter();
            param.ParameterName = "@userId";
            param.Value = userId;
            cmd.Parameters.Add(param);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new UserInfo
                {
                    UserId = userId,
                    Credit = reader.IsDBNull(0) ? null : reader.GetInt32(0),
                    CreatedDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                    DeviceType = reader.IsDBNull(2) ? null : reader.GetString(2),
                    IpAddress = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Location = reader.IsDBNull(4) ? null : reader.GetString(4)
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetUserAsync for userId: {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// User information model
    /// </summary>
    public class UserInfo
    {
        public string UserId { get; set; } = string.Empty;
        public int? Credit { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? DeviceType { get; set; }
        public string? IpAddress { get; set; }
        public string? Location { get; set; }
    }

    /// <summary>
    /// Validates if a device key (userId) exists in the database
    /// </summary>
    public async Task<bool> ValidateDeviceKeyAsync(string deviceKey)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "SELECT COUNT(1) FROM [users] WHERE [userId] = @deviceKey;"
                : "SELECT COUNT(1) FROM users WHERE userid = @deviceKey;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            var param = cmd.CreateParameter();
            param.ParameterName = "@deviceKey";
            param.Value = deviceKey;
            cmd.Parameters.Add(param);

            var result = await cmd.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in ValidateDeviceKeyAsync for deviceKey: {DeviceKey}", deviceKey);
            return false;
        }
    }

    /// <summary>
    /// Adds or subtracts credits from a user's balance
    /// </summary>
    public async Task<bool> UpdateCreditsAsync(string userId, int amount)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "UPDATE [users] SET [credit] = [credit] + @amount WHERE [userId] = @userId;"
                : @"UPDATE users SET credit = credit + @amount WHERE userid = @userId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            var paramAmount = cmd.CreateParameter();
            paramAmount.ParameterName = "@amount";
            paramAmount.Value = amount;
            cmd.Parameters.Add(paramAmount);

            var paramUserId = cmd.CreateParameter();
            paramUserId.ParameterName = "@userId";
            paramUserId.Value = userId;
            cmd.Parameters.Add(paramUserId);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in UpdateCreditsAsync for userId: {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Gets prompt description from the enhancement table by title
    /// </summary>
    public async Task<string?> GetPromptAsync(string promptTitle)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "SELECT [promptdescription] FROM [prompts] WHERE [prompttitle] = @promptTitle;"
                : @"SELECT promptdescription FROM prompts WHERE prompttitle = @promptTitle;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            var param = cmd.CreateParameter();
            param.ParameterName = "@promptTitle";
            param.Value = promptTitle;
            cmd.Parameters.Add(param);

            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return result.ToString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetPromptAsync for promptTitle: {PromptTitle}", promptTitle);
            return null;
        }
    }

    /// <summary>
    /// Updates a user's subscription data
    /// </summary>
    public async Task<bool> UpdateSubscriptionAsync(
        string userId,
        string status,
        DateTime? expiryDate,
        string? purchaseToken,
        string? productId,
        string? orderId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? @"UPDATE [users] SET
                        [subscriptionstatus] = @status,
                        [subscriptionexpiry] = @expiry,
                        [subscriptiontoken] = @token,
                        [subscriptionproductid] = @productId,
                        [subscriptionorderid] = @orderId
                    WHERE [userId] = @userId;"
                : @"UPDATE users SET
                        subscriptionstatus = @status,
                        subscriptionexpiry = @expiry,
                        subscriptiontoken = @token,
                        subscriptionproductid = @productId,
                        subscriptionorderid = @orderId
                    WHERE userid = @userId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            AddParam(cmd, "@userId", userId);
            AddParam(cmd, "@status", status);
            AddParam(cmd, "@expiry", (object?)expiryDate ?? DBNull.Value);
            AddParam(cmd, "@token", (object?)purchaseToken ?? DBNull.Value);
            AddParam(cmd, "@productId", (object?)productId ?? DBNull.Value);
            AddParam(cmd, "@orderId", (object?)orderId ?? DBNull.Value);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in UpdateSubscriptionAsync for userId: {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Gets subscription info for a user
    /// </summary>
    public async Task<SubscriptionInfo?> GetSubscriptionAsync(string userId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "SELECT [subscriptionstatus], [subscriptionexpiry], [subscriptionproductid], [subscriptionorderid], [subscriptioncreditslastgranted] FROM [users] WHERE [userId] = @userId;"
                : "SELECT subscriptionstatus, subscriptionexpiry, subscriptionproductid, subscriptionorderid, subscriptioncreditslastgranted FROM users WHERE userid = @userId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;
            AddParam(cmd, "@userId", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new SubscriptionInfo
                {
                    UserId = userId,
                    Status = reader.IsDBNull(0) ? "none" : reader.GetString(0),
                    ExpiryDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                    ProductId = reader.IsDBNull(2) ? null : reader.GetString(2),
                    OrderId = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CreditsLastGranted = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetSubscriptionAsync for userId: {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Gets all users with an active subscription (for renewal job)
    /// </summary>
    public async Task<List<SubscriptionInfo>> GetActiveSubscriptionsAsync()
    {
        var results = new List<SubscriptionInfo>();
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "SELECT [userId], [subscriptionstatus], [subscriptionexpiry], [subscriptiontoken], [subscriptionproductid], [subscriptionorderid], [subscriptioncreditslastgranted] FROM [users] WHERE [subscriptionstatus] IN ('active','grace_period','pending');"
                : "SELECT userid, subscriptionstatus, subscriptionexpiry, subscriptiontoken, subscriptionproductid, subscriptionorderid, subscriptioncreditslastgranted FROM users WHERE subscriptionstatus IN ('active','grace_period','pending');";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new SubscriptionInfo
                {
                    UserId = reader.GetString(0),
                    Status = reader.IsDBNull(1) ? "none" : reader.GetString(1),
                    ExpiryDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                    PurchaseToken = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ProductId = reader.IsDBNull(4) ? null : reader.GetString(4),
                    OrderId = reader.IsDBNull(5) ? null : reader.GetString(5),
                    CreditsLastGranted = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetActiveSubscriptionsAsync");
        }

        return results;
    }

    /// <summary>
    /// Updates the timestamp when subscription credits were last granted
    /// </summary>
    public async Task<bool> UpdateSubscriptionCreditsLastGrantedAsync(string userId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "UPDATE [users] SET [subscriptioncreditslastgranted] = @now WHERE [userId] = @userId;"
                : "UPDATE users SET subscriptioncreditslastgranted = @now WHERE userid = @userId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;
            AddParam(cmd, "@now", DateTime.UtcNow);
            AddParam(cmd, "@userId", userId);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in UpdateSubscriptionCreditsLastGrantedAsync");
            return false;
        }
    }

    // Helper to reduce parameter boilerplate
    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    public class SubscriptionInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = "none";
        public DateTime? ExpiryDate { get; set; }
        public string? PurchaseToken { get; set; }
        public string? ProductId { get; set; }
        public string? OrderId { get; set; }
        public DateTime? CreditsLastGranted { get; set; }
    }

    /// <summary>
    /// Submits user feedback to the database
    /// </summary>
    public async Task<bool> SubmitFeedbackAsync(string userId, string email, string feedbackText)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "INSERT INTO [feedback] ([userId], [email], [feedbackText], [createdAt]) VALUES (@userId, @email, @feedbackText, @createdAt);"
                : @"INSERT INTO feedback (userid, email, feedbacktext, createdat) VALUES (@userId, @email, @feedbackText, @createdAt);";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            var paramUserId = cmd.CreateParameter();
            paramUserId.ParameterName = "@userId";
            paramUserId.Value = userId;
            cmd.Parameters.Add(paramUserId);

            var paramEmail = cmd.CreateParameter();
            paramEmail.ParameterName = "@email";
            paramEmail.Value = email;
            cmd.Parameters.Add(paramEmail);

            var paramFeedbackText = cmd.CreateParameter();
            paramFeedbackText.ParameterName = "@feedbackText";
            paramFeedbackText.Value = feedbackText;
            cmd.Parameters.Add(paramFeedbackText);

            var paramCreatedAt = cmd.CreateParameter();
            paramCreatedAt.ParameterName = "@createdAt";
            paramCreatedAt.Value = DateTime.UtcNow;
            cmd.Parameters.Add(paramCreatedAt);

            await cmd.ExecuteNonQueryAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in SubmitFeedbackAsync for userId: {UserId}", userId);
            return false;
        }
    }
}
