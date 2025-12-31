# Firebase Backend Setup Guide

This guide explains how to configure the Florique API backend for async job processing with Firebase Cloud Messaging push notifications.

## Prerequisites

1. Firebase project created (see frontend FIREBASE_SETUP.md)
2. Firebase service account key downloaded
3. Database (PostgreSQL or SQL Server)

## Step 1: Run Database Migrations

### For PostgreSQL:
```bash
psql -U your_username -d your_database -f Database/migrations_postgresql.sql
```

### For SQL Server:
```bash
sqlcmd -S your_server -d your_database -i Database\migrations_sqlserver.sql
```

Or run the SQL scripts manually in your database management tool.

This will create two new tables:
- `device_tokens` - Stores Firebase device tokens for users
- `enhancement_jobs` - Stores async enhancement job information

## Step 2: Configure Firebase Credentials

You have two options for providing Firebase credentials to the API:

### Option 1: File Path (Recommended for Development)
1. Download your Firebase service account key JSON file from Firebase Console
2. Place it somewhere secure on your server (DO NOT commit to git!)
3. Set the environment variable:
   ```bash
   export FIREBASE_CREDENTIALS_PATH="/path/to/your/serviceAccountKey.json"
   ```

### Option 2: JSON String (Recommended for Production/Railway)
1. Copy the entire contents of your service account key JSON file
2. Set it as an environment variable:
   ```bash
   export FIREBASE_CREDENTIALS_JSON='{"type":"service_account","project_id":"your-project",...}'
   ```

For Railway deployment:
1. Go to your Railway project settings
2. Add a new variable: `FIREBASE_CREDENTIALS_JSON`
3. Paste the entire JSON contents as the value

## Step 3: Restart Your API

After setting the environment variable, restart your API:

```bash
dotnet run
```

Or if deployed:
```bash
# Railway will auto-deploy when you push
git push
```

## Step 4: Verify Setup

Check your API logs for:
```
Firebase initialized from credentials file
```
or
```
Firebase initialized from credentials JSON
```

If you see this warning, Firebase is not configured:
```
Firebase credentials not found. Set FIREBASE_CREDENTIALS_PATH or FIREBASE_CREDENTIALS_JSON environment variable.
```

## How It Works

### 1. Frontend Registers Device Token
When the app starts, it registers the user's Firebase device token:
```
POST /api/register-device
{
  "userId": "user-123",
  "firebaseToken": "firebase-token-abc...",
  "platform": "android"
}
```

### 2. User Starts Enhancement
When user enhances an image:
```
POST /api/enhance
{
  "userId": "user-123",
  "imagePath": "base64-image-data...",
  "backgroundStyle": "soft grey",
  "deviceToken": "firebase-token-abc..." // optional
}

Response:
{
  "success": true,
  "data": {
    "jobId": "job-456",
    "status": "processing",
    "estimatedTime": 60
  }
}
```

### 3. Backend Processes Job
- Job is created in database with status "processing"
- Background service (JobProcessingService) processes the image
- Progress is updated periodically (0%, 10%, 20%, ..., 100%)
- When complete, status changes to "completed" or "failed"

### 4. Push Notification Sent
When job completes:
- Backend sends Firebase push notification to user's device
- User receives notification even if app is closed
- Tapping notification opens app and shows result

### 5. Frontend Retrieves Result
The app can check job status and get results:

**Check Status:**
```
GET /api/jobs/{jobId}/status

Response:
{
  "success": true,
  "data": {
    "jobId": "job-456",
    "status": "completed",
    "progress": 100
  }
}
```

**Get Result:**
```
GET /api/jobs/{jobId}/result

Response:
{
  "success": true,
  "data": {
    "jobId": "job-456",
    "status": "completed",
    "imageData": <byte array>,
    "backgroundStyle": "soft grey"
  }
}
```

## API Endpoints

### POST /api/register-device
Register a Firebase device token for push notifications.

**Request:**
```json
{
  "userId": "string",
  "firebaseToken": "string",
  "platform": "android"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Device token registered successfully",
  "data": true
}
```

### POST /api/enhance
Start an async image enhancement job.

**Request:**
```json
{
  "userId": "string",
  "imagePath": "base64-image-string",
  "backgroundStyle": "string",
  "deviceToken": "string (optional)"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "jobId": "guid",
    "status": "processing",
    "estimatedTime": 60
  }
}
```

### GET /api/jobs/{jobId}/status
Check the status of a job.

**Response:**
```json
{
  "success": true,
  "data": {
    "jobId": "guid",
    "status": "processing|completed|failed",
    "progress": 0-100
  }
}
```

### GET /api/jobs/{jobId}/result
Get the result of a completed job.

**Response (Success):**
```json
{
  "success": true,
  "data": {
    "jobId": "guid",
    "status": "completed",
    "imageData": [byte array],
    "backgroundStyle": "string"
  }
}
```

**Response (Failed):**
```json
{
  "success": false,
  "message": "Error message",
  "data": {
    "jobId": "guid",
    "status": "failed",
    "errorMessage": "Error details"
  }
}
```

## Testing

### 1. Test Device Registration
```bash
curl -X POST https://your-api.com/api/register-device \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "test-user-123",
    "firebaseToken": "test-token-abc",
    "platform": "android"
  }'
```

### 2. Test Job Creation
```bash
curl -X POST https://your-api.com/api/enhance \
  -H "Content-Type: application/json" \
  -H "X-Device-Key: test-user-123" \
  -d '{
    "userId": "test-user-123",
    "imagePath": "iVBORw0KGgoAAAANS...",
    "backgroundStyle": "soft grey",
    "deviceToken": "test-token-abc"
  }'
```

### 3. Test Job Status
```bash
curl https://your-api.com/api/jobs/YOUR_JOB_ID/status
```

### 4. Test Job Result
```bash
curl https://your-api.com/api/jobs/YOUR_JOB_ID/result
```

## Production Considerations

### 1. Image Processing
Currently, the backend uses a simulated image enhancement process. You need to integrate your actual image processing:

In `JobProcessingService.cs`, replace this line:
```csharp
var enhancedImageBase64 = job.OriginalImageBase64 ?? ""; // Placeholder
```

With your actual API call:
```csharp
var enhancedImageBase64 = await CallImageEnhancementAPIAsync(
    job.OriginalImageBase64,
    job.BackgroundStyle
);
```

### 2. Job Cleanup
Old completed jobs should be cleaned up to save database space. Run this query periodically (e.g., daily cron job):

**PostgreSQL:**
```sql
DELETE FROM enhancement_jobs
WHERE status IN ('completed', 'failed')
  AND completedat < (NOW() AT TIME ZONE 'UTC') - INTERVAL '7 days';
```

**SQL Server:**
```sql
DELETE FROM [enhancement_jobs]
WHERE [status] IN ('completed', 'failed')
  AND [completedAt] < DATEADD(day, -7, GETUTCDATE());
```

### 3. Error Handling
The system automatically:
- Sends failure notifications if job fails
- Logs all errors for debugging
- Handles invalid device tokens gracefully

### 4. Scaling
For high load:
- Consider using Redis or RabbitMQ for job queuing
- Use multiple worker instances
- Implement job retry logic
- Add rate limiting per user

## Troubleshooting

### Push Notifications Not Received
1. Check Firebase credentials are set correctly
2. Verify device token is registered
3. Check API logs for Firebase errors
4. Test with Firebase Console "Send test message"

### Jobs Stuck in Processing
1. Check API logs for errors
2. Verify JobProcessingService is running (check logs for "Job Processing Service started")
3. Restart the API

### Database Errors
1. Verify migrations ran successfully
2. Check connection string is correct
3. Ensure user has proper permissions

## Environment Variables Summary

| Variable | Required | Description |
|----------|----------|-------------|
| `FIREBASE_CREDENTIALS_PATH` | One of these | Path to Firebase service account JSON file |
| `FIREBASE_CREDENTIALS_JSON` | One of these | Firebase service account JSON as string |
| `DB_TYPE` | Yes | "PostgreSQL" or "SqlServer" |
| `DATABASE_CONNECTION_STRING` | Yes | Your database connection string |

## Next Steps

1. Run database migrations
2. Set Firebase credentials environment variable
3. Deploy/restart API
4. Test with mobile app
5. Monitor logs for any errors
6. Set up job cleanup cron job

## Support

For issues or questions:
1. Check API logs first
2. Verify all environment variables are set
3. Test with curl commands above
4. Check Firebase Console for messaging errors
