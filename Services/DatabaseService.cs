using Npgsql;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using Florique.Api.Models;

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
    /// Loads background options from the database
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
                ? "SELECT [background] FROM [backgrounds] ORDER BY [background];"
                : @"SELECT background FROM backgrounds ORDER BY background;";

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
    /// Registers or updates a Firebase device token for a user
    /// </summary>
    public async Task<bool> RegisterDeviceTokenAsync(string userId, string firebaseToken, string platform = "android")
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string cmdText;
            if (_isSqlServer)
            {
                cmdText = @"
                    IF EXISTS (SELECT 1 FROM [device_tokens] WHERE [userId] = @userId)
                    BEGIN
                        UPDATE [device_tokens]
                        SET [firebaseToken] = @firebaseToken,
                            [platform] = @platform,
                            [updatedAt] = @updatedAt
                        WHERE [userId] = @userId
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [device_tokens] ([userId], [firebaseToken], [platform], [updatedAt])
                        VALUES (@userId, @firebaseToken, @platform, @updatedAt)
                    END";
            }
            else
            {
                cmdText = @"INSERT INTO device_tokens (userid, firebasetoken, platform, updatedat)
                    VALUES (@userId, @firebaseToken, @platform, @updatedAt)
                    ON CONFLICT (userid) DO UPDATE SET
                        firebasetoken = EXCLUDED.firebasetoken,
                        platform = EXCLUDED.platform,
                        updatedat = EXCLUDED.updatedat;";
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            var paramUserId = cmd.CreateParameter();
            paramUserId.ParameterName = "@userId";
            paramUserId.Value = userId;
            cmd.Parameters.Add(paramUserId);

            var paramToken = cmd.CreateParameter();
            paramToken.ParameterName = "@firebaseToken";
            paramToken.Value = firebaseToken;
            cmd.Parameters.Add(paramToken);

            var paramPlatform = cmd.CreateParameter();
            paramPlatform.ParameterName = "@platform";
            paramPlatform.Value = platform;
            cmd.Parameters.Add(paramPlatform);

            var paramUpdatedAt = cmd.CreateParameter();
            paramUpdatedAt.ParameterName = "@updatedAt";
            paramUpdatedAt.Value = DateTime.UtcNow;
            cmd.Parameters.Add(paramUpdatedAt);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in RegisterDeviceTokenAsync for userId: {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Gets the Firebase device token for a user
    /// </summary>
    public async Task<string?> GetDeviceTokenAsync(string userId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "SELECT [firebaseToken] FROM [device_tokens] WHERE [userId] = @userId;"
                : @"SELECT firebasetoken FROM device_tokens WHERE userid = @userId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            var param = cmd.CreateParameter();
            param.ParameterName = "@userId";
            param.Value = userId;
            cmd.Parameters.Add(param);

            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetDeviceTokenAsync for userId: {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Creates a new enhancement job in the database
    /// </summary>
    public async Task<bool> CreateJobAsync(EnhancementJob job)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? @"INSERT INTO [enhancement_jobs]
                    ([jobId], [userId], [status], [progress], [originalImageBase64], [backgroundStyle], [deviceToken], [createdAt])
                    VALUES (@jobId, @userId, @status, @progress, @originalImageBase64, @backgroundStyle, @deviceToken, @createdAt);"
                : @"INSERT INTO enhancement_jobs
                    (jobid, userid, status, progress, originalimageb64, backgroundstyle, devicetoken, createdat)
                    VALUES (@jobId, @userId, @status, @progress, @originalImageBase64, @backgroundStyle, @deviceToken, @createdAt);";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            cmd.Parameters.Add(CreateParam(cmd, "@jobId", job.JobId));
            cmd.Parameters.Add(CreateParam(cmd, "@userId", job.UserId));
            cmd.Parameters.Add(CreateParam(cmd, "@status", job.Status));
            cmd.Parameters.Add(CreateParam(cmd, "@progress", job.Progress));
            cmd.Parameters.Add(CreateParam(cmd, "@originalImageBase64", (object?)job.OriginalImageBase64 ?? DBNull.Value));
            cmd.Parameters.Add(CreateParam(cmd, "@backgroundStyle", job.BackgroundStyle));
            cmd.Parameters.Add(CreateParam(cmd, "@deviceToken", (object?)job.DeviceToken ?? DBNull.Value));
            cmd.Parameters.Add(CreateParam(cmd, "@createdAt", job.CreatedAt));

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in CreateJobAsync for jobId: {JobId}", job.JobId);
            return false;
        }
    }

    /// <summary>
    /// Updates a job's status and progress
    /// </summary>
    public async Task<bool> UpdateJobStatusAsync(string jobId, string status, int progress)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? "UPDATE [enhancement_jobs] SET [status] = @status, [progress] = @progress WHERE [jobId] = @jobId;"
                : "UPDATE enhancement_jobs SET status = @status, progress = @progress WHERE jobid = @jobId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            cmd.Parameters.Add(CreateParam(cmd, "@jobId", jobId));
            cmd.Parameters.Add(CreateParam(cmd, "@status", status));
            cmd.Parameters.Add(CreateParam(cmd, "@progress", progress));

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in UpdateJobStatusAsync for jobId: {JobId}", jobId);
            return false;
        }
    }

    /// <summary>
    /// Completes a job with the enhanced image
    /// </summary>
    public async Task<bool> CompleteJobAsync(string jobId, string enhancedImageBase64)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? @"UPDATE [enhancement_jobs]
                    SET [status] = 'completed', [progress] = 100, [enhancedImageBase64] = @enhancedImageBase64, [completedAt] = @completedAt
                    WHERE [jobId] = @jobId;"
                : @"UPDATE enhancement_jobs
                    SET status = 'completed', progress = 100, enhancedimageb64 = @enhancedImageBase64, completedat = @completedAt
                    WHERE jobid = @jobId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            cmd.Parameters.Add(CreateParam(cmd, "@jobId", jobId));
            cmd.Parameters.Add(CreateParam(cmd, "@enhancedImageBase64", enhancedImageBase64));
            cmd.Parameters.Add(CreateParam(cmd, "@completedAt", DateTime.UtcNow));

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in CompleteJobAsync for jobId: {JobId}", jobId);
            return false;
        }
    }

    /// <summary>
    /// Marks a job as failed
    /// </summary>
    public async Task<bool> FailJobAsync(string jobId, string errorMessage)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? @"UPDATE [enhancement_jobs]
                    SET [status] = 'failed', [errorMessage] = @errorMessage, [completedAt] = @completedAt
                    WHERE [jobId] = @jobId;"
                : @"UPDATE enhancement_jobs
                    SET status = 'failed', errormessage = @errorMessage, completedat = @completedAt
                    WHERE jobid = @jobId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            cmd.Parameters.Add(CreateParam(cmd, "@jobId", jobId));
            cmd.Parameters.Add(CreateParam(cmd, "@errorMessage", errorMessage));
            cmd.Parameters.Add(CreateParam(cmd, "@completedAt", DateTime.UtcNow));

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in FailJobAsync for jobId: {JobId}", jobId);
            return false;
        }
    }

    /// <summary>
    /// Gets a job by ID
    /// </summary>
    public async Task<EnhancementJob?> GetJobAsync(string jobId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            var cmdText = _isSqlServer
                ? @"SELECT [jobId], [userId], [status], [progress], [originalImageBase64], [enhancedImageBase64],
                    [backgroundStyle], [deviceToken], [createdAt], [completedAt], [errorMessage]
                    FROM [enhancement_jobs] WHERE [jobId] = @jobId;"
                : @"SELECT jobid, userid, status, progress, originalimageb64, enhancedimageb64,
                    backgroundstyle, devicetoken, createdat, completedat, errormessage
                    FROM enhancement_jobs WHERE jobid = @jobId;";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = cmdText;

            cmd.Parameters.Add(CreateParam(cmd, "@jobId", jobId));

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new EnhancementJob
                {
                    JobId = reader.GetString(0),
                    UserId = reader.GetString(1),
                    Status = reader.GetString(2),
                    Progress = reader.GetInt32(3),
                    OriginalImageBase64 = reader.IsDBNull(4) ? null : reader.GetString(4),
                    EnhancedImageBase64 = reader.IsDBNull(5) ? null : reader.GetString(5),
                    BackgroundStyle = reader.GetString(6),
                    DeviceToken = reader.IsDBNull(7) ? null : reader.GetString(7),
                    CreatedAt = reader.GetDateTime(8),
                    CompletedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    ErrorMessage = reader.IsDBNull(10) ? null : reader.GetString(10)
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetJobAsync for jobId: {JobId}", jobId);
            return null;
        }
    }

    /// <summary>
    /// Helper method to create a parameter
    /// </summary>
    private DbParameter CreateParam(DbCommand cmd, string name, object value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        return param;
    }
}
