# Florique API - Deployment Guide

## Overview
This API uses device-based authentication and connects to a PostgreSQL database (Neon). It's ready to deploy on Railway, Render, or Heroku.

---

## Prerequisites
- PostgreSQL database (Neon recommended - free tier available)
- Hosting platform account (Railway/Render/Heroku)

---

## Environment Variables

You must set these environment variables on your hosting platform:

### Required - Database Connection
```
DB_TYPE=PostgreSQL
POSTGRESQL_HOST=ep-jolly-bush-a79trleo-pooler.ap-southeast-2.aws.neon.tech
POSTGRESQL_PORT=5432
POSTGRESQL_USERNAME=neondb_owner
POSTGRESQL_PASSWORD=npg_UBgisLeoK9X2
POSTGRESQL_DATABASE=neondb
POSTGRESQL_SSL_MODE=VerifyFull
POSTGRESQL_CHANNEL_BINDING=Require
POSTGRESQL_TRUST_SERVER_CERTIFICATE=true
```

### Required - Security
```
CONFIG_ENCRYPTION_KEY=FLQ2025!SecureKey#ConfigEncrypt@
```

### Optional - Runtime
```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:$PORT
```

---

## Deployment Instructions

### Option 1: Railway (Recommended)

1. **Install Railway CLI** (optional)
   ```bash
   npm install -g @railway/cli
   ```

2. **Create New Project**
   - Go to https://railway.app
   - Click "New Project" → "Deploy from GitHub repo"
   - Select your repository
   - Railway auto-detects .NET and uses `railway.json`

3. **Set Environment Variables**
   - Go to project → Variables
   - Add all environment variables listed above
   - Click "Deploy"

4. **Get Your API URL**
   - Railway generates a public URL: `https://your-app.up.railway.app`
   - Use this in your mobile app

### Option 2: Render

1. **Create New Web Service**
   - Go to https://render.com
   - Click "New" → "Web Service"
   - Connect your GitHub repository

2. **Configure Service**
   - Build Command: `dotnet publish -c Release -o out`
   - Start Command: `dotnet out/Florique.Api.dll`
   - Auto-detected from `render.yaml`

3. **Set Environment Variables**
   - Go to "Environment" tab
   - Add all environment variables
   - Click "Create Web Service"

4. **Get Your API URL**
   - Render provides: `https://florique-api.onrender.com`

### Option 3: Heroku

1. **Install Heroku CLI**
   ```bash
   npm install -g heroku
   ```

2. **Login and Create App**
   ```bash
   heroku login
   heroku create florique-api
   ```

3. **Set Buildpack**
   ```bash
   heroku buildpacks:set https://github.com/jincod/dotnetcore-buildpack
   ```

4. **Set Environment Variables**
   ```bash
   heroku config:set DB_TYPE=PostgreSQL
   heroku config:set POSTGRESQL_HOST=your-host
   # ... set all other vars
   ```

5. **Deploy**
   ```bash
   git push heroku main
   ```

---

## Mobile App Integration

### 1. Generate Device ID (On First Launch)

In your MAUI app's startup code:

```csharp
public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl = "https://your-api.up.railway.app";

    public async Task<string> GetOrCreateDeviceIdAsync()
    {
        // Check if device ID exists
        var deviceId = Preferences.Get("DeviceId", null);

        if (deviceId == null)
        {
            // First launch - generate new device ID
            deviceId = Guid.NewGuid().ToString();

            // Register with API
            var registered = await RegisterDeviceAsync(deviceId);

            if (registered)
            {
                // Save for future use
                Preferences.Set("DeviceId", deviceId);
            }
        }

        return deviceId;
    }

    private async Task<bool> RegisterDeviceAsync(string deviceId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/users/register",
                new
                {
                    UserId = deviceId,
                    DeviceType = DeviceInfo.Platform.ToString(),
                    IpAddress = "", // Optional
                    Location = ""   // Optional
                });

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
```

### 2. Add Device Key to All Requests

Configure your HttpClient to include the device key:

```csharp
public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;

    public ApiService(AuthService authService)
    {
        _authService = authService;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://your-api.up.railway.app")
        };
    }

    public async Task<T> GetAsync<T>(string endpoint)
    {
        // Add device key header
        var deviceId = await _authService.GetOrCreateDeviceIdAsync();
        _httpClient.DefaultRequestHeaders.Remove("X-Device-Key");
        _httpClient.DefaultRequestHeaders.Add("X-Device-Key", deviceId);

        var response = await _httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>();
    }
}
```

### 3. Example API Calls

```csharp
// Get user credits
var credits = await _apiService.GetAsync<ApiResponse<int>>(
    $"/api/users/{deviceId}/credits"
);

// Update credits
await _apiService.PostAsync("/api/users/credits", new
{
    UserId = deviceId,
    Amount = -1  // Subtract 1 credit
});

// Get backgrounds
var backgrounds = await _apiService.GetAsync<ApiResponse<List<string>>>(
    "/api/backgrounds"
);
```

---

## Testing Deployment

### 1. Health Check
```bash
curl https://your-api.up.railway.app/api/backgrounds
```

Should return 401 (unauthorized) - this means auth is working!

### 2. Register Device
```bash
curl -X POST https://your-api.up.railway.app/api/users/register \
  -H "Content-Type: application/json" \
  -d '{"userId":"test-device-123","deviceType":"TestDevice"}'
```

Should return 200 with success message.

### 3. Test Authenticated Request
```bash
curl https://your-api.up.railway.app/api/backgrounds \
  -H "X-Device-Key: test-device-123"
```

Should return list of backgrounds.

---

## Security Features

✅ **Device Authentication** - Only registered devices can access the API
✅ **Rate Limiting** - 100 requests per minute per IP address
✅ **HTTPS Enforced** - All traffic encrypted
✅ **Database SSL** - Secure connection to PostgreSQL
✅ **Encrypted Configs** - Sensitive values encrypted in database

---

## Troubleshooting

### "Device authentication required"
- Make sure you're sending the `X-Device-Key` header
- Verify the device is registered (call `/api/users/register` first)

### "Too Many Requests (429)"
- You're hitting rate limits (100 req/min per IP)
- Wait 60 seconds or increase limit in `Program.cs`

### Database connection errors
- Verify environment variables are set correctly
- Check Neon database is accessible (not paused)
- Ensure SSL mode is `VerifyFull`

### API not starting
- Check logs on hosting platform
- Verify .NET 9 runtime is available
- Ensure all required env vars are set

---

## Cost Estimate

**Free Tier (Development)**
- Neon PostgreSQL: Free (0.5GB storage, auto-suspend)
- Railway: $5/month credit (enough for small traffic)
- Render: Free tier available (sleeps after 15min inactivity)

**Production (Low Traffic)**
- Neon: Free or ~$19/month for always-on
- Railway: ~$5-10/month
- Total: ~$5-30/month depending on traffic

---

## Next Steps

1. ✅ Set up hosting platform account
2. ✅ Set environment variables
3. ✅ Deploy API
4. ✅ Test with curl/Postman
5. ✅ Integrate device auth in mobile app
6. ✅ Test end-to-end flow
7. ⏭️ Monitor logs and performance
8. ⏭️ Set up alerts for errors

---

## Support

If you encounter issues:
1. Check hosting platform logs
2. Verify all environment variables
3. Test endpoints with curl
4. Review this guide's troubleshooting section
