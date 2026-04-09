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

        var result = await _subscriptionService.VerifySubscriptionAsync(request.PurchaseToken);

        if (!result.Success)
        {
            _logger.LogWarning("Subscription verify failed for user {UserId}: {Error}", request.UserId, result.Error);
            return StatusCode(502, new ApiResponse<SubscriptionStatusDto>
            {
                Success = false,
                Message = result.Error ?? "Could not verify subscription"
            });
        }

        var status = result.IsActive ? "active" : "expired";

        // Persist subscription to DB
        await _db.UpdateSubscriptionAsync(
            request.UserId,
            status,
            result.ExpiryDate,
            request.PurchaseToken,
            result.ProductId ?? request.ProductId,
            result.LatestOrderId);

        // Grant initial credits on first activation
        if (result.IsActive)
        {
            var existing = await _db.GetSubscriptionAsync(request.UserId);
            bool isNewSubscription = existing?.CreditsLastGranted == null;

            if (isNewSubscription)
            {
                await _db.UpdateCreditsAsync(request.UserId, 30); // Monthly credits
                await _db.UpdateSubscriptionCreditsLastGrantedAsync(request.UserId);
                _logger.LogInformation("Granted initial 30 credits to user {UserId} for new subscription", request.UserId);
            }
        }

        return Ok(new ApiResponse<SubscriptionStatusDto>
        {
            Success = true,
            Message = result.IsActive ? "Subscription activated" : "Subscription not active",
            Data = new SubscriptionStatusDto
            {
                IsActive = result.IsActive,
                Status = status,
                ExpiryDate = result.ExpiryDate,
                ProductId = result.ProductId ?? request.ProductId
            }
        });
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
                IsActive = sub.Status is "active" or "grace_period",
                Status = sub.Status,
                ExpiryDate = sub.ExpiryDate,
                ProductId = sub.ProductId
            }
        });
    }
}
