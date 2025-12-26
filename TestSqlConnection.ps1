# Test SQL Server Connection and Query Users Table
$ServerInstance = ".\SQLEXPRESS"
$Database = "florique"

try {
    # Check SQL Server services
    Write-Host "Checking SQL Server services..."
    Get-Service | Where-Object { $_.Name -like '*SQL*' -and $_.Name -like '*EXPRESS*' } |
        Select-Object Name, Status, DisplayName | Format-Table -AutoSize

    # Try to connect and query
    Write-Host "`nAttempting to connect to database..."
    $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    Write-Host "Connected successfully!`n"

    # Query table structure
    $query = @"
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'users'
ORDER BY ORDINAL_POSITION
"@

    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null

    Write-Host "Users table structure:"
    $dataset.Tables[0] | Format-Table -AutoSize

    # Query sample data
    $query2 = "SELECT TOP 5 * FROM users"
    $command2 = New-Object System.Data.SqlClient.SqlCommand($query2, $connection)
    $adapter2 = New-Object System.Data.SqlClient.SqlDataAdapter($command2)
    $dataset2 = New-Object System.Data.DataSet
    $adapter2.Fill($dataset2) | Out-Null

    Write-Host "`nSample data from users table:"
    $dataset2.Tables[0] | Format-Table -AutoSize

    $connection.Close()
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "`nConnection String: $connectionString"
}
