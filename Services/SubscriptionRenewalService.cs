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

                await _db.UpdateSubscriptionAsync(sub.UserId, result.DbStatus,
                    result.ExpiryDate, sub.PurchaseToken, sub.ProductId, result.LatestOrderId);

                _logger.LogInformation("Renewal check for user {UserId}: status={Status}, expiry={Expiry}",
                    sub.UserId, result.DbStatus, result.ExpiryDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing subscription for user {UserId}", sub.UserId);
            }
        }
    }

}
