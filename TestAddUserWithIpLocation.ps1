# Test: Add a user with IP address and location
$ServerInstance = ".\SQLEXPRESS"
$Database = "florique"

try {
    $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    Write-Host "Testing user creation with IP and location...`n" -ForegroundColor Cyan

    # Create test user
    $testUserId = "test-" + [guid]::NewGuid().ToString()
    $insertQuery = @"
INSERT INTO users (userId, credit, createdDate, deviceType, ipAddress, location)
VALUES (@userId, @credit, @createdDate, @deviceType, @ipAddress, @location)
"@

    $insertCmd = New-Object System.Data.SqlClient.SqlCommand($insertQuery, $connection)
    $insertCmd.Parameters.AddWithValue("@userId", $testUserId) | Out-Null
    $insertCmd.Parameters.AddWithValue("@credit", 50) | Out-Null
    $insertCmd.Parameters.AddWithValue("@createdDate", (Get-Date).ToUniversalTime()) | Out-Null
    $insertCmd.Parameters.AddWithValue("@deviceType", "iOS") | Out-Null
    $insertCmd.Parameters.AddWithValue("@ipAddress", "192.168.1.100") | Out-Null
    $insertCmd.Parameters.AddWithValue("@location", "New York, US") | Out-Null

    $insertCmd.ExecuteNonQuery() | Out-Null
    Write-Host "User created successfully!" -ForegroundColor Green
    Write-Host "User ID: $testUserId`n"

    # Retrieve and display the user
    $selectQuery = "SELECT * FROM users WHERE userId = @userId"
    $selectCmd = New-Object System.Data.SqlClient.SqlCommand($selectQuery, $connection)
    $selectCmd.Parameters.AddWithValue("@userId", $testUserId) | Out-Null

    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($selectCmd)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null

    Write-Host "User details:"
    $dataset.Tables[0] | Format-List

    # Show all users
    $allUsersQuery = "SELECT * FROM users ORDER BY createdDate DESC"
    $allUsersCmd = New-Object System.Data.SqlClient.SqlCommand($allUsersQuery, $connection)
    $allAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($allUsersCmd)
    $allDataset = New-Object System.Data.DataSet
    $allAdapter.Fill($allDataset) | Out-Null

    Write-Host "`nAll users:"
    $allDataset.Tables[0] | Format-Table -AutoSize

    $connection.Close()
    Write-Host "`nTest completed successfully!" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}
