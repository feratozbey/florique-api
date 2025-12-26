# Apply Migration: Add CreatedDate and DeviceType columns
$ServerInstance = ".\SQLEXPRESS"
$Database = "florique"

try {
    $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    Write-Host "Connected to database successfully.`n"

    # Step 1: Add createdDate column
    $addCreatedDate = @"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[users]') AND name = 'createdDate')
BEGIN
    ALTER TABLE [users] ADD [createdDate] DATETIME NULL;
END
"@

    $command1 = New-Object System.Data.SqlClient.SqlCommand($addCreatedDate, $connection)
    $command1.ExecuteNonQuery() | Out-Null
    Write-Host "Step 1: createdDate column checked/added"

    # Step 2: Add deviceType column
    $addDeviceType = @"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[users]') AND name = 'deviceType')
BEGIN
    ALTER TABLE [users] ADD [deviceType] NVARCHAR(100) NULL;
END
"@

    $command2 = New-Object System.Data.SqlClient.SqlCommand($addDeviceType, $connection)
    $command2.ExecuteNonQuery() | Out-Null
    Write-Host "Step 2: deviceType column checked/added"

    # Step 3: Update existing users with default creation date
    $updateExisting = "UPDATE [users] SET [createdDate] = GETUTCDATE() WHERE [createdDate] IS NULL"
    $command3 = New-Object System.Data.SqlClient.SqlCommand($updateExisting, $connection)
    $rowsAffected = $command3.ExecuteNonQuery()
    Write-Host "Step 3: Updated $rowsAffected existing user(s) with current timestamp`n"

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

    # Show updated data
    $dataQuery = "SELECT * FROM users"
    $dataCommand = New-Object System.Data.SqlClient.SqlCommand($dataQuery, $connection)
    $dataAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($dataCommand)
    $dataSet = New-Object System.Data.DataSet
    $dataAdapter.Fill($dataSet) | Out-Null

    Write-Host "`nUpdated user data:"
    $dataSet.Tables[0] | Format-Table -AutoSize

    $connection.Close()
    Write-Host "`nMigration completed successfully!" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}
