using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Florique.Api.Services;

public class SubscriptionVerificationResult
{
    public bool Success { get; set; }
    public bool IsActive { get; set; }
    public string DbStatus { get; set; } = "expired";
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

            // Map Google state → our DB status
            string dbStatus = purchase.SubscriptionState switch
            {
                "SUBSCRIPTION_STATE_ACTIVE" => "active",
                "SUBSCRIPTION_STATE_IN_GRACE_PERIOD" => "grace_period",
                "SUBSCRIPTION_STATE_CANCELED" when expiryDate.HasValue && expiryDate.Value > DateTime.UtcNow
                    => "cancelled",
                _ => "expired"
            };

            bool isActive = dbStatus is "active" or "cancelled" or "grace_period";

            _logger.LogInformation(
                "Subscription verify: state={State}, active={IsActive}, expiry={Expiry}, product={Product}",
                purchase.SubscriptionState, isActive, expiryDate, productId);

            return new SubscriptionVerificationResult
            {
                Success = true,
                IsActive = isActive,
                DbStatus = dbStatus,
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

    /// <summary>
    /// Verifies an iOS App Store subscription using the App Store Server API.
    /// </summary>
    public async Task<SubscriptionVerificationResult> VerifyIosSubscriptionAsync(string transactionId)
    {
        var bundleId = Environment.GetEnvironmentVariable("APPLE_BUNDLE_ID");
        var issuerId = Environment.GetEnvironmentVariable("APPLE_ISSUER_ID");
        var keyId = Environment.GetEnvironmentVariable("APPLE_KEY_ID");
        var privateKeyPem = Environment.GetEnvironmentVariable("APPLE_PRIVATE_KEY");

        if (string.IsNullOrEmpty(issuerId) || string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(privateKeyPem) || string.IsNullOrEmpty(bundleId))
        {
            _logger.LogError("Apple App Store Server API credentials not configured");
            return new SubscriptionVerificationResult { Success = false, Error = "Apple billing not configured" };
        }

        try
        {
            var jwt = GenerateAppleJwt(issuerId, keyId, bundleId, privateKeyPem);

            // Try production first, fall back to sandbox
            var result = await CallAppleSubscriptionApiAsync(jwt, transactionId, "https://api.storekit.itunes.apple.com");
            if (!result.Success && result.Error == "not_found")
            {
                _logger.LogInformation("Transaction not found in production, retrying with sandbox");
                result = await CallAppleSubscriptionApiAsync(jwt, transactionId, "https://api.storekit-sandbox.itunes.apple.com");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying iOS subscription transaction {TransactionId}", transactionId);
            return new SubscriptionVerificationResult { Success = false, Error = ex.Message };
        }
    }

    private string GenerateAppleJwt(string issuerId, string keyId, string bundleId, string privateKeyPem)
    {
        var pemContent = privateKeyPem
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("-----BEGIN EC PRIVATE KEY-----", "")
            .Replace("-----END EC PRIVATE KEY-----", "")
            .Replace("\n", "").Replace("\r", "").Trim();

        var keyBytes = Convert.FromBase64String(pemContent);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(keyBytes, out _);

        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = keyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuerId,
            Audience = "appstoreconnect-v1",
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { { "bid", bundleId } },
            SigningCredentials = credentials
        });
    }

    private async Task<SubscriptionVerificationResult> CallAppleSubscriptionApiAsync(string jwt, string transactionId, string baseUrl)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await http.GetAsync($"{baseUrl}/inApps/v1/subscriptions/{transactionId}");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new SubscriptionVerificationResult { Success = false, Error = "not_found" };

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Apple API returned {StatusCode}: {Error}", response.StatusCode, error);
            return new SubscriptionVerificationResult { Success = false, Error = $"Apple API error: {response.StatusCode}" };
        }

        var json = await response.Content.ReadAsStringAsync();
        var appleResponse = JsonSerializer.Deserialize<AppleSubscriptionResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var lastTransaction = appleResponse?.Data?
            .SelectMany(d => d.LastTransactions ?? [])
            .OrderByDescending(t => t.Status)
            .FirstOrDefault();

        if (lastTransaction == null)
            return new SubscriptionVerificationResult { Success = false, Error = "No transactions found" };

        DateTime? expiryDate = null;
        string? productId = null;
        try
        {
            var parts = lastTransaction.SignedTransactionInfo?.Split('.');
            if (parts?.Length >= 2)
            {
                var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                var txPayload = JsonSerializer.Deserialize<AppleTransactionPayload>(payloadJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                productId = txPayload?.ProductId;
                if (txPayload?.ExpiresDate > 0)
                    expiryDate = DateTimeOffset.FromUnixTimeMilliseconds(txPayload.ExpiresDate).UtcDateTime;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not decode JWS payload: {Error}", ex.Message);
        }

        // Apple status: 1=Active, 2=Expired, 3=BillingRetry, 4=GracePeriod, 5=Revoked
        string dbStatus = lastTransaction.Status switch
        {
            1 when expiryDate.HasValue && expiryDate.Value > DateTime.UtcNow => "active",
            3 => "grace_period",
            4 => "grace_period",
            _ when expiryDate.HasValue && expiryDate.Value > DateTime.UtcNow => "cancelled",
            _ => "expired"
        };

        bool isActive = dbStatus is "active" or "cancelled" or "grace_period";

        _logger.LogInformation("iOS subscription verify: status={Status}, dbStatus={DbStatus}, expiry={Expiry}, product={Product}",
            lastTransaction.Status, dbStatus, expiryDate, productId);

        return new SubscriptionVerificationResult
        {
            Success = true,
            IsActive = isActive,
            DbStatus = dbStatus,
            State = lastTransaction.Status.ToString(),
            ExpiryDate = expiryDate,
            ProductId = productId
        };
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }

    private class AppleSubscriptionResponse
    {
        public List<AppleSubscriptionData>? Data { get; set; }
    }

    private class AppleSubscriptionData
    {
        public List<AppleLastTransaction>? LastTransactions { get; set; }
    }

    private class AppleLastTransaction
    {
        public string? OriginalTransactionId { get; set; }
        public int Status { get; set; }
        public string? SignedTransactionInfo { get; set; }
    }

    private class AppleTransactionPayload
    {
        public string? ProductId { get; set; }
        [JsonPropertyName("expiresDate")]
        public long ExpiresDate { get; set; }
        public string? OriginalTransactionId { get; set; }
    }
}
