using Florique.Api.Models;

namespace Florique.Api.Services;

/// <summary>
/// Background service that processes enhancement jobs asynchronously
/// </summary>
public class JobProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobProcessingService> _logger;
    private readonly Dictionary<string, CancellationTokenSource> _activeJobs = new();

    public JobProcessingService(IServiceProvider serviceProvider, ILogger<JobProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Processing Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Background service is running and ready to process jobs
            // Jobs are processed on-demand when StartJobAsync is called
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("Job Processing Service stopping");
    }

    /// <summary>
    /// Starts processing a job in the background
    /// </summary>
    public async Task<string> StartJobAsync(string userId, string imageBase64, string backgroundStyle, string? deviceToken)
    {
        var jobId = Guid.NewGuid().ToString();

        using var scope = _serviceProvider.CreateScope();
        var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        // Create job in database
        var job = new EnhancementJob
        {
            JobId = jobId,
            UserId = userId,
            Status = "processing",
            Progress = 0,
            OriginalImageBase64 = imageBase64,
            BackgroundStyle = backgroundStyle,
            DeviceToken = deviceToken,
            CreatedAt = DateTime.UtcNow
        };

        await dbService.CreateJobAsync(job);
        _logger.LogInformation("Created job {JobId} for user {UserId}", jobId, userId);

        // Start processing in background
        var cts = new CancellationTokenSource();
        _activeJobs[jobId] = cts;

        _ = Task.Run(async () => await ProcessJobAsync(jobId, cts.Token), cts.Token);

        return jobId;
    }

    /// <summary>
    /// Processes an enhancement job
    /// </summary>
    private async Task ProcessJobAsync(string jobId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting to process job {JobId}", jobId);

            using var scope = _serviceProvider.CreateScope();
            var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
            var firebaseService = scope.ServiceProvider.GetRequiredService<FirebaseNotificationService>();

            // Get job details
            var job = await dbService.GetJobAsync(jobId);
            if (job == null)
            {
                _logger.LogError("Job {JobId} not found", jobId);
                return;
            }

            // Simulate image enhancement process with progress updates
            // In production, this would call your actual image enhancement API (OpenAI, etc.)

            for (int progress = 0; progress <= 100; progress += 10)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Job {JobId} cancelled", jobId);
                    return;
                }

                await dbService.UpdateJobStatusAsync(jobId, "processing", progress);
                _logger.LogInformation("Job {JobId} progress: {Progress}%", jobId, progress);

                // Simulate processing time (3 seconds total for demo)
                await Task.Delay(300, cancellationToken);
            }

            // For now, simulate a successful enhancement
            // In production, replace this with actual image enhancement logic
            var enhancedImageBase64 = job.OriginalImageBase64 ?? ""; // Placeholder - would be the actual enhanced image

            // TODO: Replace with actual image enhancement API call
            // Example:
            // var enhancedImageBase64 = await CallImageEnhancementAPIAsync(job.OriginalImageBase64, job.BackgroundStyle);

            // Mark job as completed
            await dbService.CompleteJobAsync(jobId, enhancedImageBase64);
            _logger.LogInformation("Job {JobId} completed successfully", jobId);

            // Send push notification if device token is available
            if (!string.IsNullOrEmpty(job.DeviceToken))
            {
                await firebaseService.SendEnhancementCompleteNotificationAsync(
                    job.DeviceToken,
                    jobId,
                    success: true
                );
                _logger.LogInformation("Sent completion notification for job {JobId}", jobId);
            }

            // Cleanup
            _activeJobs.Remove(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", jobId);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                var firebaseService = scope.ServiceProvider.GetRequiredService<FirebaseNotificationService>();

                var job = await dbService.GetJobAsync(jobId);

                await dbService.FailJobAsync(jobId, ex.Message);

                // Send failure notification if device token is available
                if (job != null && !string.IsNullOrEmpty(job.DeviceToken))
                {
                    await firebaseService.SendEnhancementCompleteNotificationAsync(
                        job.DeviceToken,
                        jobId,
                        success: false
                    );
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Error handling job {JobId} failure", jobId);
            }

            _activeJobs.Remove(jobId);
        }
    }

    /// <summary>
    /// Cancels a running job
    /// </summary>
    public bool CancelJob(string jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            _activeJobs.Remove(jobId);
            _logger.LogInformation("Cancelled job {JobId}", jobId);
            return true;
        }

        return false;
    }
}
