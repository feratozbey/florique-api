# Quick User Analysis Script
$ServerInstance = ".\SQLEXPRESS"
$Database = "florique"

function Execute-Query {
    param([string]$QueryText)

    try {
        $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()

        $command = New-Object System.Data.SqlClient.SqlCommand($QueryText, $connection)
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
        $dataset = New-Object System.Data.DataSet
        $adapter.Fill($dataset) | Out-Null

        $connection.Close()
        return $dataset.Tables[0]
    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
        if ($connection -and $connection.State -eq 'Open') {
            $connection.Close()
        }
        return $null
    }
}

Write-Host "=== Florique User Statistics ===" -ForegroundColor Green
Write-Host "Database: $ServerInstance\$Database`n"

# Total users
Write-Host "--- Total Users ---" -ForegroundColor Cyan
$totalUsers = Execute-Query "SELECT COUNT(*) as TotalUsers FROM users"
$totalUsers | Format-Table -AutoSize

# Users by device type
Write-Host "--- Users by Device Type ---" -ForegroundColor Cyan
$byDevice = Execute-Query @"
SELECT
    ISNULL(deviceType, 'Unknown') as DeviceType,
    COUNT(*) as UserCount
FROM users
GROUP BY deviceType
ORDER BY UserCount DESC
"@
$byDevice | Format-Table -AutoSize

# Users by location
Write-Host "--- Users by Location ---" -ForegroundColor Cyan
$byLocation = Execute-Query @"
SELECT
    ISNULL(location, 'Unknown') as Location,
    COUNT(*) as UserCount
FROM users
GROUP BY location
ORDER BY UserCount DESC
"@
$byLocation | Format-Table -AutoSize

# Recent registrations
Write-Host "--- Recent Registrations (Last 7 days) ---" -ForegroundColor Cyan
$recent = Execute-Query @"
SELECT
    userId,
    credit,
    deviceType,
    ipAddress,
    location,
    createdDate
FROM users
WHERE createdDate >= DATEADD(day, -7, GETUTCDATE())
ORDER BY createdDate DESC
"@

if ($recent.Rows.Count -eq 0) {
    Write-Host "No registrations in the last 7 days" -ForegroundColor Yellow
} else {
    $recent | Format-Table -AutoSize
}

# Credit statistics
Write-Host "--- Credit Statistics ---" -ForegroundColor Cyan
$creditStats = Execute-Query @"
SELECT
    COUNT(*) as TotalUsers,
    SUM(credit) as TotalCredits,
    AVG(credit) as AverageCredits,
    MIN(credit) as MinCredits,
    MAX(credit) as MaxCredits
FROM users
"@
$creditStats | Format-Table -AutoSize

# All users summary
Write-Host "--- All Users ---" -ForegroundColor Cyan
$allUsers = Execute-Query "SELECT * FROM users ORDER BY createdDate DESC"
$allUsers | Format-Table -AutoSize

Write-Host "`nAnalysis complete!" -ForegroundColor Green
