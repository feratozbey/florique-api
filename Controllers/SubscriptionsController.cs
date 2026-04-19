using Microsoft.AspNetCore.Mvc;
using Florique.Api.Models;
using Florique.Api.Services;

namespace Florique.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly DatabaseService _db;
    private readonly SubscriptionService _subscriptionService;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(
        DatabaseService db,
        SubscriptionService subscriptionService,
        ILogger<SubscriptionsController> logger)
    {
        _db = db;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    /// <summary>
    /// Verifies a Google Play subscription purchase and activates it for the user.
    /// Call this right after a successful subscription purchase on the mobile app.
    /// </summary>
    [HttpPost("verify-android")]
    public async Task<ActionResult<ApiResponse<SubscriptionStatusDto>>> VerifyAndroid(
        [FromBody] VerifySubscriptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.PurchaseToken))
        {
            return BadRequest(new ApiResponse<SubscriptionStatusDto>
            {
                Success = false,
                Message = "UserId and PurchaseToken are required"
            });
        }

        _logger.LogInformation("verify-android called for user {UserId}, productId: {ProductId}, tokenLength: {TokenLen}",
            request.UserId, request.ProductId, request.PurchaseToken.Length);

        var result = await _subscriptionService.VerifySubscriptionAsync(request.PurchaseToken);

        string status;
        DateTime? expiryDate;
        string? productId;

        if (!result.Success)
        {
            // Verification failed — save token as "pending" so the renewal job can retry
            _logger.LogWarning("Subscription verify failed for user {UserId}: {Error}. Saving as pending.", request.UserId, result.Error);

            status = "pending";
            expiryDate = null;
            productId = request.ProductId;
        }
        else
        {
            status = result.DbStatus;
            expiryDate = result.ExpiryDate;
            productId = result.ProductId ?? request.ProductId;

            _logger.LogInformation("Subscription verified for user {UserId}: state={State}, dbStatus={DbStatus}, expiry={Expiry}",
                request.UserId, result.State, status, expiryDate);
        }

        // Always persist to DB — renewal job will re-verify pending ones
        var saved = await _db.UpdateSubscriptionAsync(
            request.UserId,
            status,
            expiryDate,
            request.PurchaseToken,
            productId,
            result.LatestOrderId);

        _logger.LogInformation("DB update for user {UserId}: saved={Saved}, status={Status}", request.UserId, saved, status);

        bool isActive = status is "active" or "cancelled" or "grace_period";

        return Ok(new ApiResponse<SubscriptionStatusDto>
        {
            Success = true,
            Message = isActive ? "Subscription activated" : $"Subscription recorded as '{status}'",
            Data = new SubscriptionStatusDto
            {
                IsActive = isActive,
                Status = status,
                ExpiryDate = expiryDate,
                ProductId = productId
            }
        });
    }

    /// <summary>
    /// Verifies an Apple App Store subscription transaction and activates it for the user.
    /// </summary>
    [HttpPost("verify-ios")]
    public async Task<ActionResult<ApiResponse<SubscriptionStatusDto>>> VerifyIos(
        [FromBody] VerifySubscriptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.PurchaseToken))
        {
            return BadRequest(new ApiResponse<SubscriptionStatusDto>
            {
                Success = false,
                Message = "UserId and PurchaseToken are required"
            });
        }

        _logger.LogInformation("verify-ios called for user {UserId}, productId: {ProductId}, transactionIdLength: {Len}",
            request.UserId, request.ProductId, request.PurchaseToken.Length);

        var result = await _subscriptionService.VerifyIosSubscriptionAsync(request.PurchaseToken);

        string status;
        DateTime? expiryDate;
        string? productId;

        if (!result.Success)
        {
            _logger.LogWarning("iOS subscription verify failed for user {UserId}: {Error}. Saving as pending.", request.UserId, result.Error);
            status = "pending";
            expiryDate = null;
            productId = request.ProductId;
        }
        else
        {
            status = result.DbStatus;
            expiryDate = result.ExpiryDate;
            productId = result.ProductId ?? request.ProductId;

            _logger.LogInformation("iOS subscription verified for user {UserId}: status={Status}, expiry={Expiry}",
                request.UserId, status, expiryDate);
        }

        var saved = await _db.UpdateSubscriptionAsync(
            request.UserId,
            status,
            expiryDate,
            request.PurchaseToken,
            productId,
            result.LatestOrderId);

        _logger.LogInformation("DB update for user {UserId}: saved={Saved}, status={Status}", request.UserId, saved, status);

        bool isActive = status is "active" or "cancelled" or "grace_period";

        return Ok(new ApiResponse<SubscriptionStatusDto>
        {
            Success = true,
            Message = isActive ? "Subscription activated" : $"Subscription recorded as '{status}'",
            Data = new SubscriptionStatusDto
            {
                IsActive = isActive,
                Status = status,
                ExpiryDate = expiryDate,
                ProductId = productId
            }
        });
    }

    /// <summary>
    /// Manually triggers the subscription renewal check for all active subscriptions.
    /// Protected by X-Admin-Secret header.
    /// </summary>
    [HttpPost("process-renewals")]
    public async Task<IActionResult> TriggerRenewal(
        [FromHeader(Name = "X-Admin-Secret")] string? adminSecret)
    {
        var expectedSecret = Environment.GetEnvironmentVariable("ADMIN_SECRET");
        if (string.IsNullOrEmpty(expectedSecret) || adminSecret != expectedSecret)
            return Unauthorized(new { message = "Invalid or missing admin secret" });

        var activeSubscriptions = await _db.GetActiveSubscriptionsAsync();
        _logger.LogInformation("Manual renewal triggered: {Count} subscriptions to process", activeSubscriptions.Count);

        int processed = 0;
        var results = new List<object>();

        foreach (var sub in activeSubscriptions)
        {
            if (string.IsNullOrEmpty(sub.PurchaseToken)) continue;

            try
            {
                var isIos = sub.UserId.StartsWith("ios_", StringComparison.OrdinalIgnoreCase);
                var result = isIos
                    ? await _subscriptionService.VerifyIosSubscriptionAsync(sub.PurchaseToken)
                    : await _subscriptionService.VerifySubscriptionAsync(sub.PurchaseToken);

                if (!result.Success)
                {
                    _logger.LogWarning("Could not verify subscription for user {UserId}: {Error}", sub.UserId, result.Error);
                    results.Add(new { userId = sub.UserId, success = false, error = result.Error });
                    continue;
                }

                await _db.UpdateSubscriptionAsync(sub.UserId, result.DbStatus,
                    result.ExpiryDate, sub.PurchaseToken, sub.ProductId, result.LatestOrderId);

                processed++;
                results.Add(new { userId = sub.UserId, success = true, status = result.DbStatus, expiry = result.ExpiryDate });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing subscription for user {UserId}", sub.UserId);
                results.Add(new { userId = sub.UserId, success = false, error = ex.Message });
            }
        }

        return Ok(new { processed, total = activeSubscriptions.Count, results });
    }

    /// <summary>
    /// Gets the current subscription status for a user.
    /// </summary>
    [HttpGet("{userId}/status")]
    public async Task<ActionResult<ApiResponse<SubscriptionStatusDto>>> GetStatus(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new ApiResponse<SubscriptionStatusDto> { Success = false, Message = "UserId required" });

        var sub = await _db.GetSubscriptionAsync(userId);

        if (sub == null)
            return NotFound(new ApiResponse<SubscriptionStatusDto> { Success = false, Message = "User not found" });

        return Ok(new ApiResponse<SubscriptionStatusDto>
        {
            Success = true,
            Data = new SubscriptionStatusDto
            {
                IsActive = sub.Status is "active" or "cancelled" or "grace_period",
                Status = sub.Status,
                ExpiryDate = sub.ExpiryDate,
                ProductId = sub.ProductId
            }
        });
    }
}
