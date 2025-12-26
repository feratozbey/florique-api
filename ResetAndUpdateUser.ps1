# Script to reset user's tracking fields and update with real data
param(
    [Parameter(Mandatory=$true)]
    [string]$UserId
)

$ServerInstance = ".\SQLEXPRESS"
$Database = "florique"

Write-Host "Resetting user's tracking fields..." -ForegroundColor Cyan

try {
    # Step 1: Clear the existing test data (set back to NULL)
    $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    $resetQuery = @"
UPDATE [users]
SET [deviceType] = NULL,
    [ipAddress] = NULL,
    [location] = NULL
WHERE [userId] = @userId
"@

    $resetCommand = New-Object System.Data.SqlClient.SqlCommand($resetQuery, $connection)
    $resetCommand.Parameters.AddWithValue('@userId', $UserId) | Out-Null
    $resetCommand.ExecuteNonQuery() | Out-Null

    Write-Host "✓ Cleared existing test data" -ForegroundColor Yellow

    $connection.Close()

    # Step 2: Fetch real IP and location
    Write-Host "Fetching your real public IP and location..." -ForegroundColor Cyan

    $locationData = Invoke-RestMethod -Uri "https://ipapi.co/json/" -Method Get

    $realIp = $locationData.ip
    $city = $locationData.city
    $country = $locationData.country_code
    $location = "$city, $country"

    Write-Host "✓ Your real public IP: $realIp" -ForegroundColor Green
    Write-Host "✓ Your location: $location" -ForegroundColor Green
    Write-Host ""

    # Step 3: Update via API with real data
    $apiUrl = "http://localhost:5217/api/users/register"
    $deviceType = "Windows"

    Write-Host "Updating user with real data..." -ForegroundColor Cyan

    $body = @{
        userId = $UserId
        deviceType = $deviceType
        ipAddress = $realIp
        location = $location
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $body -ContentType "application/json"

    if ($response.success) {
        Write-Host "✓ User updated successfully!" -ForegroundColor Green
    } else {
        $msg = $response.message
        Write-Host "✗ Update failed: $msg" -ForegroundColor Red
    }

    # Step 4: Verify in database
    Write-Host ""
    Write-Host "Final result in database:" -ForegroundColor Cyan

    $connection2 = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection2.Open()

    $verifyQuery = 'SELECT * FROM users WHERE userId = @userId'
    $verifyCommand = New-Object System.Data.SqlClient.SqlCommand($verifyQuery, $connection2)
    $verifyCommand.Parameters.AddWithValue('@userId', $UserId) | Out-Null

    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($verifyCommand)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null

    Write-Host ""
    $dataset.Tables[0] | Format-Table -AutoSize

    $connection2.Close()

    Write-Host "Update complete!" -ForegroundColor Green

} catch {
    $errorMsg = $_.Exception.Message
    Write-Host "Error: $errorMsg" -ForegroundColor Red
}
