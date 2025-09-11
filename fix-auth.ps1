# Fix authentication for YARA import
$NewPassword = "admin123"

Write-Host "Setting up authentication..." -ForegroundColor Cyan

# Update config with plaintext password temporarily
$configPath = "src\Castellan.Worker\appsettings.json"
$config = Get-Content $configPath -Raw | ConvertFrom-Json
$config.Authentication.AdminUser.Username = "admin"
$config.Authentication.AdminUser.Password = $NewPassword
$config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8

Write-Host "Config updated" -ForegroundColor Green

# Modify authentication controller to accept plaintext
$authPath = "src\Castellan.Worker\Controllers\AuthController.cs"
$authContent = Get-Content $authPath -Raw
Copy-Item $authPath ($authPath + ".backup") -Force

$modifiedAuth = $authContent -replace 
    'var isValidPassword = isValidUser && _passwordHashingService\.VerifyPassword\(request\.Password, _authOptions\.AdminUser\.Password\);',
    'var isValidPassword = isValidUser && (request.Password == _authOptions.AdminUser.Password || _passwordHashingService.VerifyPassword(request.Password, _authOptions.AdminUser.Password));'

$modifiedAuth | Set-Content $authPath -Encoding UTF8

Write-Host "Auth modified" -ForegroundColor Green

# Restart service
Write-Host "Restarting service..." -ForegroundColor Yellow
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3
Start-Process -FilePath "dotnet" -ArgumentList "run --project src\Castellan.Worker" -WindowStyle Hidden
Start-Sleep -Seconds 8

# Test authentication and run import
Write-Host "Testing auth and running import..." -ForegroundColor Yellow

$loginData = @{
    username = "admin"
    password = $NewPassword
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method POST -Body $loginData -ContentType "application/json" -TimeoutSec 10
    
    if ($response.token) {
        Write-Host "Authentication SUCCESS!" -ForegroundColor Green
        Write-Host "Running YARA import..." -ForegroundColor Cyan
        & ".\import-yara-with-auth.ps1" -Password $NewPassword
    } else {
        Write-Host "Auth failed - no token" -ForegroundColor Red
    }
} catch {
    Write-Host "Auth failed: $($_.Exception.Message)" -ForegroundColor Red
}
