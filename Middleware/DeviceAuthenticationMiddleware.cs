using Florique.Api.Services;

namespace Florique.Api.Middleware;

/// <summary>
/// Middleware to validate device authentication
/// </summary>
public class DeviceAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DeviceAuthenticationMiddleware> _logger;

    public DeviceAuthenticationMiddleware(RequestDelegate next, ILogger<DeviceAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, DatabaseService databaseService)
    {
        // Skip authentication for certain paths
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Allow these endpoints without authentication
        if (path.Contains("/swagger") ||
            path.Contains("/health") ||
            path == "/" ||
            path.Contains("/api/users/register")) // Registration needs to be open
        {
            await _next(context);
            return;
        }

        // Check for device key header
        if (!context.Request.Headers.TryGetValue("X-Device-Key", out var deviceKey) ||
            string.IsNullOrWhiteSpace(deviceKey))
        {
            _logger.LogWarning("Request to {Path} missing X-Device-Key header", path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new {
                success = false,
                message = "Device authentication required. Please include X-Device-Key header."
            });
            return;
        }

        // Validate device key exists in database
        var isValid = await databaseService.ValidateDeviceKeyAsync(deviceKey!);
        if (!isValid)
        {
            _logger.LogWarning("Invalid device key attempted: {DeviceKey}", deviceKey);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new {
                success = false,
                message = "Invalid device key. Please register your device first."
            });
            return;
        }

        // Add device key to HttpContext items for use in controllers
        context.Items["DeviceKey"] = deviceKey.ToString();

        await _next(context);
    }
}

/// <summary>
/// Extension method to add device authentication middleware to the pipeline
/// </summary>
public static class DeviceAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseDeviceAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<DeviceAuthenticationMiddleware>();
    }
}
