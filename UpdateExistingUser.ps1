# Script to update an existing user with device type, IP address, and location
param(
    [Parameter(Mandatory=$true)]
    [string]$UserId,

    [string]$DeviceType = "Windows",
    [string]$IpAddress = "192.168.1.200",
    [string]$Location = "Test Location, US"
)

$apiUrl = "http://localhost:5217/api/users/register"

Write-Host "Updating user via API..." -ForegroundColor Cyan
Write-Host "User ID: $UserId"
Write-Host "Device Type: $DeviceType"
Write-Host "IP Address: $IpAddress"
Write-Host "Location: $Location"
Write-Host ""

try {
    $body = @{
        userId = $UserId
        deviceType = $DeviceType
        ipAddress = $IpAddress
        location = $Location
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $body -ContentType "application/json"

    if ($response.success) {
        Write-Host "Success: User updated successfully!" -ForegroundColor Green
    } else {
        $msg = $response.message
        Write-Host "Failed: $msg" -ForegroundColor Red
    }
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "Error calling API: $errorMsg" -ForegroundColor Red
    Write-Host "Make sure the API is running on http://localhost:5217" -ForegroundColor Yellow
}

# Query the database to verify
Write-Host ""
Write-Host "Querying database to verify update..." -ForegroundColor Cyan

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
    Write-Host "User details from database:"
    $dataset.Tables[0] | Format-List

    $connection.Close()
} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "Error querying database: $errorMsg" -ForegroundColor Red
}
