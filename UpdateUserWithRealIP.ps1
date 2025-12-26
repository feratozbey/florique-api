# Script to update user with REAL public IP and location
param(
    [Parameter(Mandatory=$true)]
    [string]$UserId
)

Write-Host "Fetching your real public IP and location..." -ForegroundColor Cyan

try {
    # Get real public IP and location from ipapi.co (same service the mobile app uses)
    $locationData = Invoke-RestMethod -Uri "https://ipapi.co/json/" -Method Get

    $realIp = $locationData.ip
    $city = $locationData.city
    $country = $locationData.country_code
    $location = "$city, $country"

    Write-Host "Your real public IP: $realIp" -ForegroundColor Green
    Write-Host "Your location: $location" -ForegroundColor Green
    Write-Host ""

    # Detect device type
    $deviceType = "Windows"

    # Update user via API
    $apiUrl = "http://localhost:5217/api/users/register"

    Write-Host "Updating user in database..." -ForegroundColor Cyan

    $body = @{
        userId = $UserId
        deviceType = $deviceType
        ipAddress = $realIp
        location = $location
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $body -ContentType "application/json"

    if ($response.success) {
        Write-Host "Success: User updated with real data!" -ForegroundColor Green
    } else {
        $msg = $response.message
        Write-Host "Failed: $msg" -ForegroundColor Red
    }

} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "Error: $errorMsg" -ForegroundColor Red
}

# Verify in database
Write-Host ""
Write-Host "Verifying update in database..." -ForegroundColor Cyan

$ServerInstance = ".\SQLEXPRESS"
$Database = "florique"

try {
    $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    $query = 'SELECT * FROM users WHERE userId = @userId'
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $command.Parameters.AddWithValue('@userId', $UserId) | Out-Null

    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null

    Write-Host ""
    Write-Host "User record in database:"
    $dataset.Tables[0] | Format-Table -AutoSize

    $connection.Close()
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "Error querying database: $errorMsg" -ForegroundColor Red
}
