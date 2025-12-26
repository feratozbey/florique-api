# Database Query Helper for Florique
# Usage: .\QueryDatabase.ps1 -Query "SELECT * FROM users"
# Or run without parameters for interactive mode

param(
    [string]$Query = ""
)

$ServerInstance = ".\SQLEXPRESS"
$Database = "florique"

function Execute-Query {
    param([string]$QueryText)

    try {
        $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()

        $command = New-Object System.Data.SqlClient.SqlCommand($QueryText, $connection)

        if ($QueryText.Trim().ToUpper().StartsWith("SELECT")) {
            # For SELECT queries, return results
            $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
            $dataset = New-Object System.Data.DataSet
            $adapter.Fill($dataset) | Out-Null

            if ($dataset.Tables[0].Rows.Count -eq 0) {
                Write-Host "Query returned no results." -ForegroundColor Yellow
            } else {
                $dataset.Tables[0] | Format-Table -AutoSize
                Write-Host "Rows returned: $($dataset.Tables[0].Rows.Count)" -ForegroundColor Green
            }
        } else {
            # For INSERT, UPDATE, DELETE
            $rowsAffected = $command.ExecuteNonQuery()
            Write-Host "Query executed successfully. Rows affected: $rowsAffected" -ForegroundColor Green
        }

        $connection.Close()
    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
        if ($connection -and $connection.State -eq 'Open') {
            $connection.Close()
        }
    }
}

# Quick access commands
function Show-AllUsers {
    Write-Host "`n=== All Users ===" -ForegroundColor Cyan
    Execute-Query "SELECT * FROM users ORDER BY createdDate DESC"
}

function Show-TableStructure {
    param([string]$TableName = "users")
    Write-Host "`n=== Table Structure: $TableName ===" -ForegroundColor Cyan
    Execute-Query "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$TableName' ORDER BY ORDINAL_POSITION"
}

function Show-Backgrounds {
    Write-Host "`n=== Backgrounds ===" -ForegroundColor Cyan
    Execute-Query "SELECT * FROM backgrounds ORDER BY background"
}

# Main execution
if ($Query -ne "") {
    Execute-Query $Query
} else {
    # Interactive mode
    Write-Host "=== Florique Database Query Tool ===" -ForegroundColor Green
    Write-Host "Connected to: $ServerInstance\$Database`n"

    while ($true) {
        Write-Host "`nAvailable commands:" -ForegroundColor Yellow
        Write-Host "  1. Show all users"
        Write-Host "  2. Show table structure"
        Write-Host "  3. Show backgrounds"
        Write-Host "  4. Custom query"
        Write-Host "  5. Exit"

        $choice = Read-Host "`nEnter your choice (1-5)"

        switch ($choice) {
            "1" { Show-AllUsers }
            "2" { Show-TableStructure }
            "3" { Show-Backgrounds }
            "4" {
                $customQuery = Read-Host "Enter your SQL query"
                Execute-Query $customQuery
            }
            "5" {
                Write-Host "Goodbye!" -ForegroundColor Green
                exit
            }
            default {
                Write-Host "Invalid choice. Please enter 1-5." -ForegroundColor Red
            }
        }
    }
}
