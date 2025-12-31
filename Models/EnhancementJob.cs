namespace Florique.Api.Models;

/// <summary>
/// Represents an asynchronous image enhancement job
/// </summary>
public class EnhancementJob
{
    public string JobId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = "processing"; // processing, completed, failed
    public int Progress { get; set; } = 0;
    public string? OriginalImageBase64 { get; set; }
    public string? EnhancedImageBase64 { get; set; }
    public string BackgroundStyle { get; set; } = string.Empty;
    public string? DeviceToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request to start a new enhancement job
/// </summary>
public class StartEnhancementJobRequest
{
    public string UserId { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty; // Base64 encoded image
    public string BackgroundStyle { get; set; } = string.Empty;
    public string? DeviceToken { get; set; }
}

/// <summary>
/// Response when starting an enhancement job
/// </summary>
public class StartEnhancementJobResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "processing";
    public int EstimatedTime { get; set; } = 60; // seconds
}

/// <summary>
/// Response when checking job status
/// </summary>
public class JobStatusResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "processing";
    public int Progress { get; set; } = 0;
}

/// <summary>
/// Response when getting job result
/// </summary>
public class JobResultResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "completed";
    public byte[]? ImageData { get; set; }
    public string? BackgroundStyle { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request to register a device token
/// </summary>
public class RegisterDeviceTokenRequest
{
    public string UserId { get; set; } = string.Empty;
    public string FirebaseToken { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
}
