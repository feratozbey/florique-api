# User Management Helper for Florique Database
# Provides easy commands to view, add, update, and delete users

$ServerInstance = ".\SQLEXPRESS"
$Database = "florique"

function Get-Connection {
    $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    return $connection
}

function Execute-Query {
    param([string]$QueryText, [hashtable]$Parameters = @{})

    try {
        $connection = Get-Connection
        $command = New-Object System.Data.SqlClient.SqlCommand($QueryText, $connection)

        foreach ($key in $Parameters.Keys) {
            $param = $command.Parameters.AddWithValue("@$key", $Parameters[$key])
        }

        if ($QueryText.Trim().ToUpper().StartsWith("SELECT")) {
            $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
            $dataset = New-Object System.Data.DataSet
            $adapter.Fill($dataset) | Out-Null
            $connection.Close()
            return $dataset.Tables[0]
        } else {
            $rowsAffected = $command.ExecuteNonQuery()
            $connection.Close()
            return $rowsAffected
        }
    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
        if ($connection -and $connection.State -eq 'Open') {
            $connection.Close()
        }
        return $null
    }
}

function Get-AllUsers {
    Write-Host "`n=== All Users ===" -ForegroundColor Cyan
    $result = Execute-Query "SELECT * FROM users ORDER BY createdDate DESC"
    $result | Format-Table -AutoSize
    Write-Host "Total users: $($result.Rows.Count)" -ForegroundColor Green
}

function Get-User {
    param([string]$UserId)

    if ([string]::IsNullOrWhiteSpace($UserId)) {
        Write-Host "Error: UserId is required" -ForegroundColor Red
        return
    }

    Write-Host "`n=== User Details ===" -ForegroundColor Cyan
    $result = Execute-Query "SELECT * FROM users WHERE userId = @userId" @{userId = $UserId}

    if ($result.Rows.Count -eq 0) {
        Write-Host "User not found: $UserId" -ForegroundColor Yellow
    } else {
        $result | Format-Table -AutoSize
    }
}

function Add-User {
    param(
        [string]$UserId,
        [int]$Credit = 0,
        [string]$DeviceType = $null,
        [string]$IpAddress = $null,
        [string]$Location = $null
    )

    if ([string]::IsNullOrWhiteSpace($UserId)) {
        Write-Host "Error: UserId is required" -ForegroundColor Red
        return
    }

    Write-Host "`nAdding new user..." -ForegroundColor Cyan

    $params = @{
        userId = $UserId
        credit = $Credit
        createdDate = (Get-Date).ToUniversalTime()
    }

    $columns = @("userId", "credit", "createdDate")
    $values = @("@userId", "@credit", "@createdDate")

    if (![string]::IsNullOrWhiteSpace($DeviceType)) {
        $params.deviceType = $DeviceType
        $columns += "deviceType"
        $values += "@deviceType"
    }

    if (![string]::IsNullOrWhiteSpace($IpAddress)) {
        $params.ipAddress = $IpAddress
        $columns += "ipAddress"
        $values += "@ipAddress"
    }

    if (![string]::IsNullOrWhiteSpace($Location)) {
        $params.location = $Location
        $columns += "location"
        $values += "@location"
    }

    $query = "INSERT INTO users ($($columns -join ', ')) VALUES ($($values -join ', '))"

    $result = Execute-Query $query $params

    if ($result -ne $null) {
        Write-Host "User added successfully!" -ForegroundColor Green
        Get-User $UserId
    }
}

function Update-UserCredits {
    param(
        [string]$UserId,
        [int]$Amount
    )

    if ([string]::IsNullOrWhiteSpace($UserId)) {
        Write-Host "Error: UserId is required" -ForegroundColor Red
        return
    }

    Write-Host "`nUpdating credits for user $UserId..." -ForegroundColor Cyan

    $result = Execute-Query "UPDATE users SET credit = credit + @amount WHERE userId = @userId" @{
        userId = $UserId
        amount = $Amount
    }

    if ($result -gt 0) {
        Write-Host "Credits updated successfully! (Change: $Amount)" -ForegroundColor Green
        Get-User $UserId
    } else {
        Write-Host "User not found: $UserId" -ForegroundColor Yellow
    }
}

function Set-UserDeviceType {
    param(
        [string]$UserId,
        [string]$DeviceType
    )

    if ([string]::IsNullOrWhiteSpace($UserId)) {
        Write-Host "Error: UserId is required" -ForegroundColor Red
        return
    }

    Write-Host "`nUpdating device type for user $UserId..." -ForegroundColor Cyan

    $result = Execute-Query "UPDATE users SET deviceType = @deviceType WHERE userId = @userId" @{
        userId = $UserId
        deviceType = $DeviceType
    }

    if ($result -gt 0) {
        Write-Host "Device type updated successfully!" -ForegroundColor Green
        Get-User $UserId
    } else {
        Write-Host "User not found: $UserId" -ForegroundColor Yellow
    }
}

function Set-UserIpAddress {
    param(
        [string]$UserId,
        [string]$IpAddress
    )

    if ([string]::IsNullOrWhiteSpace($UserId)) {
        Write-Host "Error: UserId is required" -ForegroundColor Red
        return
    }

    Write-Host "`nUpdating IP address for user $UserId..." -ForegroundColor Cyan

    $result = Execute-Query "UPDATE users SET ipAddress = @ipAddress WHERE userId = @userId" @{
        userId = $UserId
        ipAddress = $IpAddress
    }

    if ($result -gt 0) {
        Write-Host "IP address updated successfully!" -ForegroundColor Green
        Get-User $UserId
    } else {
        Write-Host "User not found: $UserId" -ForegroundColor Yellow
    }
}

function Set-UserLocation {
    param(
        [string]$UserId,
        [string]$Location
    )

    if ([string]::IsNullOrWhiteSpace($UserId)) {
        Write-Host "Error: UserId is required" -ForegroundColor Red
        return
    }

    Write-Host "`nUpdating location for user $UserId..." -ForegroundColor Cyan

    $result = Execute-Query "UPDATE users SET location = @location WHERE userId = @userId" @{
        userId = $UserId
        location = $Location
    }

    if ($result -gt 0) {
        Write-Host "Location updated successfully!" -ForegroundColor Green
        Get-User $UserId
    } else {
        Write-Host "User not found: $UserId" -ForegroundColor Yellow
    }
}

function Remove-User {
    param([string]$UserId)

    if ([string]::IsNullOrWhiteSpace($UserId)) {
        Write-Host "Error: UserId is required" -ForegroundColor Red
        return
    }

    $confirm = Read-Host "Are you sure you want to delete user '$UserId'? (yes/no)"

    if ($confirm.ToLower() -eq "yes") {
        Write-Host "`nDeleting user..." -ForegroundColor Cyan

        $result = Execute-Query "DELETE FROM users WHERE userId = @userId" @{userId = $UserId}

        if ($result -gt 0) {
            Write-Host "User deleted successfully!" -ForegroundColor Green
        } else {
            Write-Host "User not found: $UserId" -ForegroundColor Yellow
        }
    } else {
        Write-Host "Deletion cancelled." -ForegroundColor Yellow
    }
}

# Show menu
function Show-Menu {
    Write-Host "`n=== Florique User Management ===" -ForegroundColor Green
    Write-Host "Connected to: $ServerInstance\$Database`n"

    Write-Host "Available commands:"
    Write-Host "  Get-AllUsers" -ForegroundColor Yellow
    Write-Host "  Get-User -UserId 'user-id'" -ForegroundColor Yellow
    Write-Host "  Add-User -UserId 'user-id' -Credit 10 -DeviceType 'iOS' -IpAddress '192.168.1.1' -Location 'New York, US'" -ForegroundColor Yellow
    Write-Host "  Update-UserCredits -UserId 'user-id' -Amount 5" -ForegroundColor Yellow
    Write-Host "  Set-UserDeviceType -UserId 'user-id' -DeviceType 'Android'" -ForegroundColor Yellow
    Write-Host "  Set-UserIpAddress -UserId 'user-id' -IpAddress '192.168.1.1'" -ForegroundColor Yellow
    Write-Host "  Set-UserLocation -UserId 'user-id' -Location 'London, UK'" -ForegroundColor Yellow
    Write-Host "  Remove-User -UserId 'user-id'" -ForegroundColor Yellow
    Write-Host ""
}

# Auto-show menu when script is run
Show-Menu
