# Florique Database Management Guide

## Overview
The Florique API now supports tracking user creation date and device type. This guide shows you how to interact with your SQL Server database.

## Database Configuration
- **Server**: `.\SQLEXPRESS`
- **Database**: `florique`
- **Authentication**: Windows Authentication (Integrated Security)

## Database Schema

### Users Table
| Column       | Type          | Nullable | Description                                    |
|--------------|---------------|----------|------------------------------------------------|
| id           | int           | No       | Primary key (auto-increment)                   |
| userId       | nvarchar(255) | No       | Unique user identifier (GUID)                  |
| credit       | int           | No       | User's credit balance                          |
| createdDate  | datetime      | Yes      | Timestamp when user was created (UTC)          |
| deviceType   | nvarchar(100) | Yes      | Device type used during registration           |
| ipAddress    | nvarchar(45)  | Yes      | IP address from registration (supports IPv6)   |
| location     | nvarchar(255) | Yes      | Geographic location (e.g., "New York, US")     |

### Other Tables
- **backgrounds**: Stores available background options
- **prompts**: Stores prompt templates

## PowerShell Management Scripts

### 1. CheckUsers.ps1 - User Statistics & Analysis
Shows comprehensive user statistics including totals, device types, recent registrations, and credit statistics.

**Usage:**
```powershell
powershell.exe -ExecutionPolicy Bypass -File CheckUsers.ps1
```

**Output:**
- Total user count
- Users grouped by device type
- Recent registrations (last 7 days)
- Credit statistics (total, average, min, max)
- Full user list

---

### 2. QueryDatabase.ps1 - Database Query Tool
Execute custom SQL queries or use pre-built commands.

**Usage:**
```powershell
# Interactive mode
powershell.exe -ExecutionPolicy Bypass -File QueryDatabase.ps1

# Direct query
powershell.exe -ExecutionPolicy Bypass -File QueryDatabase.ps1 -Query "SELECT * FROM users"
```

**Example Queries:**
```sql
-- Get all users
SELECT * FROM users ORDER BY createdDate DESC

-- Get users by device type
SELECT * FROM users WHERE deviceType = 'iOS'

-- Count users by device
SELECT deviceType, COUNT(*) as Count FROM users GROUP BY deviceType

-- Users created in last 30 days
SELECT * FROM users WHERE createdDate >= DATEADD(day, -30, GETUTCDATE())

-- Users with low credits
SELECT * FROM users WHERE credit < 10 ORDER BY credit ASC
```

---

### 3. ManageUsers.ps1 - User Management Functions
Provides PowerShell functions for managing users.

**Usage:**
```powershell
# First, load the script to make functions available
. .\ManageUsers.ps1

# Then use the functions:
```

**Available Functions:**

#### Get-AllUsers
Lists all users in the database.
```powershell
Get-AllUsers
```

#### Get-User
Get details for a specific user.
```powershell
Get-User -UserId "d78fca12-3813-4f7a-bed3-432411c7accd"
```

#### Add-User
Add a new user to the database.
```powershell
# With all fields
Add-User -UserId "new-user-guid" -Credit 10 -DeviceType "iOS" -IpAddress "192.168.1.100" -Location "New York, US"

# With minimal fields
Add-User -UserId "new-user-guid" -Credit 5

# With device type only
Add-User -UserId "new-user-guid" -Credit 10 -DeviceType "iOS"
```

#### Update-UserCredits
Add or subtract credits from a user.
```powershell
# Add 10 credits
Update-UserCredits -UserId "user-guid" -Amount 10

# Subtract 5 credits
Update-UserCredits -UserId "user-guid" -Amount -5
```

#### Set-UserDeviceType
Update the device type for a user.
```powershell
Set-UserDeviceType -UserId "user-guid" -DeviceType "Android"
```

#### Set-UserIpAddress
Update the IP address for a user.
```powershell
Set-UserIpAddress -UserId "user-guid" -IpAddress "192.168.1.100"
```

#### Set-UserLocation
Update the location for a user.
```powershell
Set-UserLocation -UserId "user-guid" -Location "London, UK"
```

#### Remove-User
Delete a user (requires confirmation).
```powershell
Remove-User -UserId "user-guid"
```

---

## API Endpoints

### Register User
**POST** `/api/users/register`

**Request:**
```json
{
  "userId": "d78fca12-3813-4f7a-bed3-432411c7accd",
  "deviceType": "iOS",
  "ipAddress": "192.168.1.100",
  "location": "New York, US"
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

**Field Examples:**
- **deviceType**: `"iOS"`, `"Android"`, `"Web"`, `"Windows"`, `"macOS"` (optional)
- **ipAddress**: `"192.168.1.100"`, `"2001:0db8:85a3::8a2e:0370:7334"` (optional, supports IPv4 and IPv6)
- **location**: `"New York, US"`, `"London, UK"`, `"Tokyo, Japan"` (optional)

---

### Get User Details
**GET** `/api/users/{userId}`

**Response:**
```json
{
  "success": true,
  "data": {
    "userId": "d78fca12-3813-4f7a-bed3-432411c7accd",
    "credits": 4,
    "createdDate": "2025-12-17T18:19:12Z",
    "deviceType": "iOS",
    "ipAddress": "192.168.1.100",
    "location": "New York, US"
  }
}
```

---

### Get User Credits
**GET** `/api/users/{userId}/credits`

**Response:**
```json
{
  "success": true,
  "data": 4
}
```

---

### Update User Credits
**POST** `/api/users/credits`

**Request:**
```json
{
  "userId": "d78fca12-3813-4f7a-bed3-432411c7accd",
  "amount": 10
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

---

## Direct SQL Server Access

You can also use SQL Server Management Studio (SSMS) or Azure Data Studio:

**Connection Details:**
- Server: `.\SQLEXPRESS` or `localhost\SQLEXPRESS`
- Authentication: Windows Authentication
- Database: `florique`

---

## Common Tasks

### Check how many users registered from each device type
```powershell
powershell.exe -ExecutionPolicy Bypass -File QueryDatabase.ps1 -Query "SELECT deviceType, COUNT(*) as UserCount FROM users GROUP BY deviceType"
```

### Find users who registered today
```powershell
powershell.exe -ExecutionPolicy Bypass -File QueryDatabase.ps1 -Query "SELECT * FROM users WHERE CAST(createdDate AS DATE) = CAST(GETUTCDATE() AS DATE)"
```

### Update an existing user's device type
```powershell
. .\ManageUsers.ps1
Set-UserDeviceType -UserId "your-user-id" -DeviceType "Android"
```

### Add test users
```powershell
. .\ManageUsers.ps1
Add-User -UserId "test-user-1" -Credit 100 -DeviceType "iOS" -IpAddress "192.168.1.10" -Location "New York, US"
Add-User -UserId "test-user-2" -Credit 50 -DeviceType "Android" -IpAddress "10.0.0.5" -Location "London, UK"
Add-User -UserId "test-user-3" -Credit 75 -DeviceType "Web" -IpAddress "172.16.0.1" -Location "Tokyo, Japan"
```

### Find users from a specific location
```powershell
powershell.exe -ExecutionPolicy Bypass -File QueryDatabase.ps1 -Query "SELECT * FROM users WHERE location LIKE '%New York%'"
```

### Find users by IP address pattern
```powershell
powershell.exe -ExecutionPolicy Bypass -File QueryDatabase.ps1 -Query "SELECT * FROM users WHERE ipAddress LIKE '192.168.%'"
```

### Get location statistics
```powershell
powershell.exe -ExecutionPolicy Bypass -File QueryDatabase.ps1 -Query "SELECT location, COUNT(*) as UserCount FROM users WHERE location IS NOT NULL GROUP BY location ORDER BY UserCount DESC"
```

---

## Migration Information

The database migration has already been applied. If you need to apply it to a different database or environment, use:

```powershell
# For SQL Server
sqlcmd -S .\SQLEXPRESS -d florique -E -i Migrations\001_AddUserCreationFields_SqlServer.sql

# Or use the PowerShell script
powershell.exe -ExecutionPolicy Bypass -File ApplyMigration.ps1
```

---

## Troubleshooting

### SQL Server service not running
Check service status and start if needed:
```powershell
Get-Service | Where-Object { $_.Name -like '*SQL*EXPRESS*' }
Start-Service MSSQL$SQLEXPRESS
```

### Connection timeout
Ensure SQL Server is configured to allow connections and the instance name is correct.

### Permission denied
Make sure your Windows user has appropriate permissions on the SQL Server instance.

---

## Files Created

| File                                          | Purpose                                    |
|-----------------------------------------------|-------------------------------------------|
| `CheckUsers.ps1`                              | User statistics and analysis (with location/IP stats) |
| `QueryDatabase.ps1`                           | Custom SQL query execution                |
| `ManageUsers.ps1`                             | User management functions (with IP/location) |
| `TestSqlConnection.ps1`                       | Test database connectivity                |
| `ApplyMigration.ps1`                          | Apply migration 001 (createdDate, deviceType) |
| `ApplyMigration002.ps1`                       | Apply migration 002 (ipAddress, location) |
| `Migrations/001_AddUserCreationFields_*.sql`  | Migration 001 scripts (SQL Server & PostgreSQL)|
| `Migrations/002_AddIpAndLocation_*.sql`       | Migration 002 scripts (SQL Server & PostgreSQL)|
| `Migrations/README.md`                        | Migration documentation                   |
| `DATABASE_MANAGEMENT_GUIDE.md`                | This comprehensive guide                  |

---

## Next Steps

1. Test the API endpoints using the provided `.http` file or Postman
2. Update your mobile/web apps to send `deviceType`, `ipAddress`, and `location` during registration
3. Use the PowerShell scripts to monitor user registrations, locations, and device distributions
4. Consider adding indexes for better query performance:
   ```sql
   CREATE INDEX IX_Users_DeviceType ON users(deviceType);
   CREATE INDEX IX_Users_CreatedDate ON users(createdDate DESC);
   CREATE INDEX IX_Users_Location ON users(location);
   CREATE INDEX IX_Users_IpAddress ON users(ipAddress);
   ```

---

**Need help?** All scripts include error handling and clear output messages. Run them to see what they do!
