using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace Florique.Api.Services;

/// <summary>
/// Service for sending push notifications via Firebase Cloud Messaging
/// </summary>
public class FirebaseNotificationService
{
    private readonly ILogger<FirebaseNotificationService> _logger;
    private readonly ConfigurationService _configService;
    private bool _initialized = false;

    public FirebaseNotificationService(ILogger<FirebaseNotificationService> logger, ConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;
        InitializeFirebase();
    }

    /// <summary>
    /// Initializes Firebase Admin SDK
    /// </summary>
    private void InitializeFirebase()
    {
        try
        {
            // Check if already initialized
            if (FirebaseApp.DefaultInstance != null)
            {
                _initialized = true;
                _logger.LogInformation("Firebase already initialized");
                return;
            }

            // Try to get Firebase credentials from environment variable
            var firebaseCredentialsPath = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_PATH");

            if (!string.IsNullOrEmpty(firebaseCredentialsPath) && File.Exists(firebaseCredentialsPath))
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(firebaseCredentialsPath)
                });
                _initialized = true;
                _logger.LogInformation("Firebase initialized from credentials file");
            }
            else
            {
                // Try to get credentials from environment variable JSON
                var firebaseCredentialsJson = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_JSON");

                if (!string.IsNullOrEmpty(firebaseCredentialsJson))
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromJson(firebaseCredentialsJson)
                    });
                    _initialized = true;
                    _logger.LogInformation("Firebase initialized from credentials JSON");
                }
                else
                {
                    _logger.LogWarning("Firebase credentials not found. Set FIREBASE_CREDENTIALS_PATH or FIREBASE_CREDENTIALS_JSON environment variable.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Firebase");
        }
    }

    /// <summary>
    /// Sends a push notification when an enhancement job completes
    /// </summary>
    public async Task<bool> SendEnhancementCompleteNotificationAsync(string deviceToken, string jobId, bool success)
    {
        if (!_initialized)
        {
            _logger.LogWarning("Firebase not initialized, cannot send notification");
            return false;
        }

        try
        {
            var message = new Message
            {
                Token = deviceToken,
                Notification = new Notification
                {
                    Title = success ? "Image Ready! ✨" : "Enhancement Failed",
                    Body = success
                        ? "Your enhanced flower image is ready to view and save."
                        : "There was an error enhancing your image. Please try again."
                },
                Data = new Dictionary<string, string>
                {
                    { "jobId", jobId },
                    { "success", success.ToString().ToLower() }
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            _logger.LogInformation("Successfully sent notification for job {JobId}. Response: {Response}", jobId, response);
            return true;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(ex, "Firebase error sending notification for job {JobId}. Error code: {ErrorCode}", jobId, ex.MessagingErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification for job {JobId}", jobId);
            return false;
        }
    }

    /// <summary>
    /// Sends a push notification with custom title and body
    /// </summary>
    public async Task<bool> SendNotificationAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null)
    {
        if (!_initialized)
        {
            _logger.LogWarning("Firebase not initialized, cannot send notification");
            return false;
        }

        try
        {
            var message = new Message
            {
                Token = deviceToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data ?? new Dictionary<string, string>()
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            _logger.LogInformation("Successfully sent notification. Response: {Response}", response);
            return true;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(ex, "Firebase error sending notification. Error code: {ErrorCode}", ex.MessagingErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification");
            return false;
        }
    }
}
