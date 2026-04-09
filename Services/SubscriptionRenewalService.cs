namespace Florique.Api.Services;

/// <summary>
/// Background service that runs every 24 hours to check active subscriptions,
/// grant monthly credits, and expire cancelled subscriptions.
/// </summary>
public class SubscriptionRenewalService : BackgroundService
{
    private readonly DatabaseService _db;
    private readonly SubscriptionService _subscriptionService;
    private readonly ILogger<SubscriptionRenewalService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);
    private const int MonthlyCredits = 30; // Credits granted per billing period

    public SubscriptionRenewalService(
        DatabaseService db,
        SubscriptionService subscriptionService,
        ILogger<SubscriptionRenewalService> logger)
    {
        _db = db;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SubscriptionRenewalService started");

        // Small delay on startup so DB is ready
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessSubscriptionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in SubscriptionRenewalService");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessSubscriptionsAsync()
    {
        _logger.LogInformation("Processing active subscriptions...");

        var activeSubscriptions = await _db.GetActiveSubscriptionsAsync();
        _logger.LogInformation("Found {Count} active subscriptions to check", activeSubscriptions.Count);

        foreach (var sub in activeSubscriptions)
        {
            if (string.IsNullOrEmpty(sub.PurchaseToken))
                continue;

            try
            {
                var result = await _subscriptionService.VerifySubscriptionAsync(sub.PurchaseToken);

                if (!result.Success)
                {
                    _logger.LogWarning("Could not verify subscription for user {UserId}: {Error}", sub.UserId, result.Error);
                    continue;
                }

                if (!result.IsActive)
                {
                    // Subscription expired or cancelled — update status
                    await _db.UpdateSubscriptionAsync(sub.UserId, "expired",
                        result.ExpiryDate, sub.PurchaseToken, sub.ProductId, result.LatestOrderId);
                    _logger.LogInformation("Marked subscription as expired for user {UserId}", sub.UserId);
                    continue;
                }

                // Subscription is still active — check if we should grant credits for a new billing period
                bool shouldGrantCredits = ShouldGrantCredits(sub, result.ExpiryDate);

                if (shouldGrantCredits)
                {
                    var creditsGranted = await _db.UpdateCreditsAsync(sub.UserId, MonthlyCredits);
                    if (creditsGranted)
                    {
                        await _db.UpdateSubscriptionCreditsLastGrantedAsync(sub.UserId);
                        _logger.LogInformation(
                            "Granted {Credits} credits to user {UserId} for subscription renewal (new expiry: {Expiry})",
                            MonthlyCredits, sub.UserId, result.ExpiryDate);
                    }
                }

                // Always update expiry in case it changed
                await _db.UpdateSubscriptionAsync(sub.UserId,
                    result.IsActive ? "active" : "expired",
                    result.ExpiryDate, sub.PurchaseToken, sub.ProductId, result.LatestOrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing subscription for user {UserId}", sub.UserId);
            }
        }
    }

    /// <summary>
    /// Credits should be granted when the subscription expiry has advanced
    /// beyond when we last granted credits (i.e. a new billing period started).
    /// </summary>
    private static bool ShouldGrantCredits(DatabaseService.SubscriptionInfo sub, DateTime? newExpiry)
    {
        if (newExpiry == null) return false;

        // Never granted before → grant now
        if (sub.CreditsLastGranted == null) return true;

        // New expiry is beyond the last time we granted credits → new billing period
        return newExpiry.Value > sub.CreditsLastGranted.Value.AddDays(25);
    }
}
