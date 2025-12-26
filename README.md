# Florique API

REST API for the Florique floral image enhancement application.

## Overview

The Florique API provides endpoints for user management, credit tracking, and background options for the Florique mobile app. It serves as a secure intermediary between the mobile application and the database.

## Architecture

- **Framework**: ASP.NET Core 9.0 Web API
- **Databases Supported**: SQL Server and PostgreSQL
- **Authentication**: Open (add your own authentication as needed)
- **Documentation**: Swagger/OpenAPI

## API Endpoints

### Backgrounds

#### Get All Backgrounds
```http
GET /api/backgrounds
```

Returns a list of available background styles for image enhancement.

**Response:**
```json
{
  "success": true,
  "message": null,
  "data": ["bedroom", "cream", "funeral", "home", "kitchen", ...]
}
```

### Users

#### Register User
```http
POST /api/users/register
```

Creates a new user if they don't already exist.

**Request Body:**
```json
{
  "userId": "string"
}
```

**Response:**
```json
{
  "success": true,
  "message": "User registered successfully",
  "data": true
}
```

#### Get User Credits
```http
GET /api/users/{userId}/credits
```

Returns the credit balance for a specific user.

**Response:**
```json
{
  "success": true,
  "message": null,
  "data": 6
}
```

#### Get User Information
```http
GET /api/users/{userId}
```

Returns complete user information including credits.

**Response:**
```json
{
  "success": true,
  "message": null,
  "data": {
    "userId": "test-user-123",
    "credits": 6
  }
}
```

#### Update Credits
```http
POST /api/users/credits
```

Updates a user's credit balance (positive or negative amount).

**Request Body:**
```json
{
  "userId": "string",
  "amount": 0
}
```

**Response:**
```json
{
  "success": true,
  "message": "Credits updated successfully",
  "data": true
}
```

## Setup Instructions

### Prerequisites

- .NET 9.0 SDK
- SQL Server or PostgreSQL database
- `.env` file with database credentials

### Environment Variables

Create a `.env` file in the `Florique.Api` directory with the following:

```env
# Database Configuration
DB_TYPE=SqlServer

# SQL Server Configuration (Local)
SQLSERVER_SERVER=.\SQLEXPRESS
SQLSERVER_DATABASE=florique
SQLSERVER_INTEGRATED_SECURITY=true
SQLSERVER_TRUST_SERVER_CERTIFICATE=true

# OR PostgreSQL Configuration
POSTGRESQL_HOST=your-host
POSTGRESQL_PORT=5432
POSTGRESQL_USERNAME=your-username
POSTGRESQL_PASSWORD=your-password
POSTGRESQL_DATABASE=your-database
POSTGRESQL_SSL_MODE=Require
POSTGRESQL_TRUST_SERVER_CERTIFICATE=true
```

### Running Locally

1. Navigate to the API directory:
```bash
cd Florique.Api
```

2. Build the project:
```bash
dotnet build
```

3. Run the API:
```bash
dotnet run
```

The API will start at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `http://localhost:5000/swagger`

### Testing the API

Use Swagger UI or test with curl:

```bash
# Get backgrounds
curl http://localhost:5000/api/backgrounds

# Register a user
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{"userId":"test-user"}'

# Get credits
curl http://localhost:5000/api/users/test-user/credits

# Update credits
curl -X POST http://localhost:5000/api/users/credits \
  -H "Content-Type: application/json" \
  -d '{"userId":"test-user","amount":50}'
```

## Deployment

### Azure App Service

1. Create an Azure App Service
2. Set environment variables in Application Settings
3. Deploy using:

```bash
dotnet publish -c Release
# Upload to Azure
```

### Docker

Build and run with Docker:

```bash
docker build -t florique-api .
docker run -p 5000:8080 --env-file .env florique-api
```

## Mobile App Integration

The mobile app automatically uses the API when `ApiService` is registered. Update the API URL in `ApiService.cs`:

```csharp
// For local Windows development
_baseUrl = "http://localhost:5000";

// For Android emulator
_baseUrl = "http://10.0.2.2:5000";

// For production
_baseUrl = "https://your-api.azurewebsites.net";
```

## Security Recommendations

Before deploying to production:

1. **Add Authentication**: Implement JWT or API key authentication
2. **Rate Limiting**: Add rate limiting to prevent abuse
3. **HTTPS Only**: Disable HTTP in production
4. **CORS**: Restrict CORS to specific origins
5. **Logging**: Add comprehensive logging and monitoring
6. **Secrets**: Use Azure Key Vault or similar for secrets

## Project Structure

```
Florique.Api/
├── Controllers/
│   ├── BackgroundsController.cs
│   └── UsersController.cs
├── Models/
│   └── UserDto.cs
├── Services/
│   ├── ConfigurationService.cs
│   └── DatabaseService.cs
├── Program.cs
├── Florique.Api.csproj
└── .env
```

## Error Handling

All endpoints return a consistent response format:

```json
{
  "success": boolean,
  "message": "string or null",
  "data": object or null
}
```

HTTP Status Codes:
- `200 OK`: Success
- `400 Bad Request`: Invalid input
- `404 Not Found`: Resource not found
- `500 Internal Server Error`: Server error

## Support

For issues or questions, check the logs or Swagger documentation at `/swagger`.
"# florique-api" 
