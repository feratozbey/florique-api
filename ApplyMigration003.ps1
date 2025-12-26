# Apply Migration 003: Create configurations table and seed with .env values
$ServerInstance = ".\SQLEXPRESS"
$Database = "florique"
$EnvFile = ".env"

Write-Host "=== Migration 003: Create Configurations Table ===" -ForegroundColor Cyan
Write-Host ""

try {
    # Read .env file to get current API key
    $envApiKey = $null
    $envEndpoint = "https://api.openai.com/v1/images/edits"
    $envModel = "gpt-image-1-mini"
    $envProductId = "credits_50_pack"

    if (Test-Path $EnvFile) {
        Write-Host "Reading current configuration from .env file..." -ForegroundColor Yellow
        $envContent = Get-Content $EnvFile
        foreach ($line in $envContent) {
            if ($line -match '^OPENAI_API_KEY=(.+)$') {
                $envApiKey = $matches[1].Trim()
            }
            elseif ($line -match '^OPENAI_API_ENDPOINT=(.+)$') {
                $envEndpoint = $matches[1].Trim()
            }
            elseif ($line -match '^OPENAI_MODEL=(.+)$') {
                $envModel = $matches[1].Trim()
            }
            elseif ($line -match '^PRODUCT_ID_50_CREDITS=(.+)$') {
                $envProductId = $matches[1].Trim()
            }
        }
        Write-Host "✓ Configuration values read from .env" -ForegroundColor Green
        Write-Host "  - API Key: $($envApiKey.Substring(0, 10))..." -ForegroundColor Gray
        Write-Host "  - Endpoint: $envEndpoint" -ForegroundColor Gray
        Write-Host "  - Model: $envModel" -ForegroundColor Gray
        Write-Host ""
    }

    # Connect to database
    $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    Write-Host "Connected to database successfully." -ForegroundColor Green
    Write-Host ""

    # Step 1: Create table
    Write-Host "Step 1: Creating configurations table..." -ForegroundColor Cyan

    $createTable = 'IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = ''configurations'')
BEGIN
    CREATE TABLE [configurations] (
        [id] INT IDENTITY(1,1) PRIMARY KEY,
        [configKey] NVARCHAR(100) NOT NULL UNIQUE,
        [configValue] NVARCHAR(500) NULL,
        [description] NVARCHAR(255) NULL,
        [category] NVARCHAR(50) NULL,
        [isEncrypted] BIT DEFAULT 0,
        [createdDate] DATETIME DEFAULT GETUTCDATE(),
        [updatedDate] DATETIME DEFAULT GETUTCDATE()
    );
END'

    $cmd1 = New-Object System.Data.SqlClient.SqlCommand($createTable, $connection)
    $cmd1.ExecuteNonQuery() | Out-Null
    Write-Host "✓ Configurations table created" -ForegroundColor Green

    # Step 2: Create index
    Write-Host "Step 2: Creating index..." -ForegroundColor Cyan

    $createIndex = 'IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = ''IX_Configurations_ConfigKey'')
BEGIN
    CREATE INDEX IX_Configurations_ConfigKey ON [configurations]([configKey]);
END'

    $cmd2 = New-Object System.Data.SqlClient.SqlCommand($createIndex, $connection)
    $cmd2.ExecuteNonQuery() | Out-Null
    Write-Host "✓ Index created" -ForegroundColor Green

    # Step 3: Insert configuration values
    Write-Host "Step 3: Inserting configuration values..." -ForegroundColor Cyan

    $insertConfigs = 'MERGE INTO [configurations] AS target
USING (VALUES
    (''OPENAI_API_KEY'', @apiKey, ''OpenAI API Key for image generation'', ''OpenAI'', 1),
    (''OPENAI_API_ENDPOINT'', @endpoint, ''OpenAI API endpoint URL'', ''OpenAI'', 0),
    (''OPENAI_MODEL'', @model, ''OpenAI model to use for image generation'', ''OpenAI'', 0),
    (''DB_TYPE'', ''SqlServer'', ''Database type (SqlServer or PostgreSQL)'', ''Database'', 0),
    (''PRODUCT_ID_50_CREDITS'', @productId, ''In-app purchase product ID for 50 credits'', ''InAppPurchase'', 0)
) AS source ([configKey], [configValue], [description], [category], [isEncrypted])
ON target.[configKey] = source.[configKey]
WHEN MATCHED THEN
    UPDATE SET
        [configValue] = COALESCE(target.[configValue], source.[configValue]),
        [description] = source.[description],
        [category] = source.[category],
        [isEncrypted] = source.[isEncrypted],
        [updatedDate] = GETUTCDATE()
WHEN NOT MATCHED THEN
    INSERT ([configKey], [configValue], [description], [category], [isEncrypted])
    VALUES (source.[configKey], source.[configValue], source.[description], source.[category], source.[isEncrypted]);'

    $cmd3 = New-Object System.Data.SqlClient.SqlCommand($insertConfigs, $connection)
    $cmd3.Parameters.AddWithValue('@apiKey', $(if ($envApiKey) { $envApiKey } else { [DBNull]::Value })) | Out-Null
    $cmd3.Parameters.AddWithValue('@endpoint', $envEndpoint) | Out-Null
    $cmd3.Parameters.AddWithValue('@model', $envModel) | Out-Null
    $cmd3.Parameters.AddWithValue('@productId', $envProductId) | Out-Null

    $cmd3.ExecuteNonQuery() | Out-Null
    Write-Host "✓ Configuration values inserted/updated" -ForegroundColor Green

    # Verify the configuration table
    Write-Host ""
    Write-Host "Verifying configurations table..." -ForegroundColor Cyan

    $verifyQuery = "SELECT [configKey], [configValue], [description], [category], [isEncrypted] FROM [configurations] ORDER BY [category], [configKey]"
    $verifyCmd = New-Object System.Data.SqlClient.SqlCommand($verifyQuery, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($verifyCmd)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null

    Write-Host ""
    Write-Host "Configurations in database:" -ForegroundColor Yellow
    $dataset.Tables[0] | Format-Table -AutoSize

    $connection.Close()

    Write-Host ""
    Write-Host "Migration 003 completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Update ConfigurationService to read from database" -ForegroundColor White
    Write-Host "2. Test the API to ensure it reads configurations correctly" -ForegroundColor White
    Write-Host "3. Optionally remove sensitive data from .env file" -ForegroundColor White

} catch {
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    if ($connection -and $connection.State -eq 'Open') {
        $connection.Close()
    }
}
