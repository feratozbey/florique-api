namespace Florique.Api.Models;

/// <summary>
/// Data transfer object for user information
/// </summary>
public class UserDto
{
    public string UserId { get; set; } = string.Empty;
    public int Credits { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? DeviceType { get; set; }
    public string? IpAddress { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Request model for user registration
/// </summary>
public class RegisterUserRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? DeviceType { get; set; }
    public string? IpAddress { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Request model for updating credits
/// </summary>
public class UpdateCreditsRequest
{
    public string UserId { get; set; } = string.Empty;
    public int Amount { get; set; }
}

/// <summary>
/// Request model for submitting feedback
/// </summary>
public class SubmitFeedbackRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FeedbackText { get; set; } = string.Empty;
}

/// <summary>
/// Request to verify a Google Play subscription purchase
/// </summary>
public class VerifySubscriptionRequest
{
    public string UserId { get; set; } = string.Empty;
    public string PurchaseToken { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
}

/// <summary>
/// Subscription status returned to the mobile app
/// </summary>
public class SubscriptionStatusDto
{
    public bool IsActive { get; set; }
    public string Status { get; set; } = "none";
    public DateTime? ExpiryDate { get; set; }
    public string? ProductId { get; set; }
}

/// <summary>
/// Response model for API operations
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}
