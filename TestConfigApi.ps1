# Test what the API is actually returning for OPENAI_MODEL configuration
# This script makes an authenticated request to your Railway API

$apiUrl = "https://florique-api-production-6a8a.up.railway.app"
$deviceKey = "test-device-" + (Get-Random -Maximum 99999)

Write-Host "=== Testing Configuration API ===" -ForegroundColor Cyan
Write-Host "API URL: $apiUrl" -ForegroundColor Gray
Write-Host "Device Key: $deviceKey" -ForegroundColor Gray
Write-Host ""

try {
    # Register device first
    Write-Host "Registering test device..." -ForegroundColor Yellow

    $registerBody = @{
        userId = $deviceKey
        deviceType = "Test"
    } | ConvertTo-Json

    Invoke-RestMethod -Uri "$apiUrl/api/users/register" -Method Post -Body $registerBody -ContentType "application/json" | Out-Null
    Write-Host "Device registered successfully" -ForegroundColor Green
    Write-Host ""

    # Test OPENAI_MODEL endpoint
    Write-Host "Fetching OPENAI_MODEL configuration..." -ForegroundColor Yellow

    $headers = @{
        "X-Device-Key" = $deviceKey
        "Content-Type" = "application/json"
    }

    $response = Invoke-RestMethod -Uri "$apiUrl/api/configurations/OPENAI_MODEL" -Headers $headers -Method Get

    Write-Host "Response received:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 5 | Write-Host

    Write-Host ""
    Write-Host "===================================" -ForegroundColor Yellow
    Write-Host "MODEL VALUE: $($response.value)" -ForegroundColor Cyan
    Write-Host "===================================" -ForegroundColor Yellow
    Write-Host ""

    # Test OpenAI config endpoint
    Write-Host "Fetching full OpenAI configuration..." -ForegroundColor Yellow
    $openaiConfig = Invoke-RestMethod -Uri "$apiUrl/api/configurations/openai" -Headers $headers -Method Get

    Write-Host "Full OpenAI Config:" -ForegroundColor Green
    $openaiConfig | ConvertTo-Json -Depth 5 | Write-Host

} catch {
    Write-Host "Error occurred:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response body:" -ForegroundColor Yellow
        Write-Host $responseBody -ForegroundColor Yellow
    }
}
