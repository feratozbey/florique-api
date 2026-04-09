using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;

namespace Florique.Api.Services;

public class SubscriptionVerificationResult
{
    public bool Success { get; set; }
    public bool IsActive { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public string? ProductId { get; set; }
    public string? LatestOrderId { get; set; }
    public string? Error { get; set; }
}

public class SubscriptionService
{
    private const string PackageName = "com.artromeos.florique.app";
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(ILogger<SubscriptionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Verifies a Google Play subscription purchase token.
    /// Returns subscription state and expiry date.
    /// </summary>
    public async Task<SubscriptionVerificationResult> VerifySubscriptionAsync(string purchaseToken)
    {
        var serviceAccountJson = Environment.GetEnvironmentVariable("GOOGLE_PLAY_SERVICE_ACCOUNT_JSON");

        if (string.IsNullOrEmpty(serviceAccountJson))
        {
            _logger.LogError("GOOGLE_PLAY_SERVICE_ACCOUNT_JSON environment variable is not set");
            return new SubscriptionVerificationResult { Success = false, Error = "Billing service not configured" };
        }

        try
        {
            var credential = GoogleCredential.FromJson(serviceAccountJson)
                .CreateScoped(AndroidPublisherService.ScopeConstants.Androidpublisher);

            var service = new AndroidPublisherService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Florique"
            });

            var purchase = await service.Purchases.Subscriptionsv2
                .Get(PackageName, purchaseToken)
                .ExecuteAsync();

            bool isActive = purchase.SubscriptionState is
                "SUBSCRIPTION_STATE_ACTIVE" or
                "SUBSCRIPTION_STATE_IN_GRACE_PERIOD";

            DateTime? expiryDate = null;
            string? productId = null;

            if (purchase.LineItems?.Count > 0)
            {
                var item = purchase.LineItems[0];
                productId = item.ProductId;

                // ExpiryTime is a raw RFC3339 string; parse it
                var expiryRaw = item.ExpiryTimeRaw;
                if (!string.IsNullOrEmpty(expiryRaw) &&
                    DateTimeOffset.TryParse(expiryRaw, out var dto))
                {
                    expiryDate = dto.UtcDateTime;
                }
            }

            _logger.LogInformation(
                "Subscription verify: state={State}, active={IsActive}, expiry={Expiry}, product={Product}",
                purchase.SubscriptionState, isActive, expiryDate, productId);

            return new SubscriptionVerificationResult
            {
                Success = true,
                IsActive = isActive,
                State = purchase.SubscriptionState ?? string.Empty,
                ExpiryDate = expiryDate,
                ProductId = productId,
                LatestOrderId = purchase.LatestOrderId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying subscription purchase token");
            return new SubscriptionVerificationResult { Success = false, Error = ex.Message };
        }
    }
}
