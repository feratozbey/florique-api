# Apply Migration 002: Add IpAddress and Location columns
$ServerInstance = ".\SQLEXPRESS"
$Database = "florique"

try {
    $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    Write-Host "Connected to database successfully.`n"

    # Step 1: Add ipAddress column
    $addIpAddress = @"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[users]') AND name = 'ipAddress')
BEGIN
    ALTER TABLE [users] ADD [ipAddress] NVARCHAR(45) NULL;
END
"@

    $command1 = New-Object System.Data.SqlClient.SqlCommand($addIpAddress, $connection)
    $command1.ExecuteNonQuery() | Out-Null
    Write-Host "Step 1: ipAddress column checked/added"

    # Step 2: Add location column
    $addLocation = @"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[users]') AND name = 'location')
BEGIN
    ALTER TABLE [users] ADD [location] NVARCHAR(255) NULL;
END
"@

    $command2 = New-Object System.Data.SqlClient.SqlCommand($addLocation, $connection)
    $command2.ExecuteNonQuery() | Out-Null
    Write-Host "Step 2: location column checked/added`n"

    # Verify the changes
    $verifyQuery = @"
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'users'
ORDER BY ORDINAL_POSITION
"@

    $verifyCommand = New-Object System.Data.SqlClient.SqlCommand($verifyQuery, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($verifyCommand)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null

    Write-Host "Updated users table structure:"
    $dataset.Tables[0] | Format-Table -AutoSize

    # Show sample data
    $dataQuery = "SELECT * FROM users"
    $dataCommand = New-Object System.Data.SqlClient.SqlCommand($dataQuery, $connection)
    $dataAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($dataCommand)
    $dataSet = New-Object System.Data.DataSet
    $dataAdapter.Fill($dataSet) | Out-Null

    Write-Host "`nCurrent user data:"
    $dataSet.Tables[0] | Format-Table -AutoSize

    $connection.Close()
    Write-Host "`nMigration 002 completed successfully!" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}
