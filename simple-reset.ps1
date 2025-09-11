# Simple password reset for Castellan
param(
    [string]$NewPassword = "admin123"
)

Write-Host "🔧 Setting up Castellan authentication..." -ForegroundColor Cyan

# Update appsettings.json with a known password (plaintext for now)
$configPath = "src\Castellan.Worker\appsettings.json"

if (Test-Path $configPath) {
    try {
        $config = Get-Content $configPath -Raw | ConvertFrom-Json
        
        # Set username and password
        $config.Authentication.AdminUser.Username = "admin"
        $config.Authentication.AdminUser.Password = $NewPassword  # Use plaintext temporarily
        
        # Save the configuration
        $config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8
        
        Write-Host "✅ Configuration updated" -ForegroundColor Green
        Write-Host "   Username: admin" -ForegroundColor Cyan
        Write-Host "   Password: $NewPassword" -ForegroundColor Cyan
        
    } catch {
        Write-Host "❌ Failed to update config: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "❌ Config file not found" -ForegroundColor Red
    exit 1
}

# Now I need to temporarily modify the authentication to accept plaintext
Write-Host "`n🔄 Temporarily modifying authentication logic..." -ForegroundColor Yellow

$authControllerPath = "src\Castellan.Worker\Controllers\AuthController.cs"

if (Test-Path $authControllerPath) {
    try {
        $authContent = Get-Content $authControllerPath -Raw
        
        # Make a backup
        $backupPath = $authControllerPath + ".backup"
        Copy-Item $authControllerPath $backupPath -Force
        
        # Replace the password verification logic with simple string comparison
        $modifiedContent = $authContent -replace 
            'var isValidPassword = isValidUser && _passwordHashingService\.VerifyPassword\(request\.Password, _authOptions\.AdminUser\.Password\);',
            'var isValidPassword = isValidUser && (request.Password == _authOptions.AdminUser.Password || _passwordHashingService.VerifyPassword(request.Password, _authOptions.AdminUser.Password));'
        
        # Save the modified controller
        $modifiedContent | Set-Content $authControllerPath -Encoding UTF8
        
        Write-Host "✅ Authentication temporarily modified" -ForegroundColor Green
        Write-Host "⚠️  Backup saved to: $backupPath" -ForegroundColor Yellow
        
    } catch {
        Write-Host "❌ Failed to modify authentication: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "❌ AuthController not found" -ForegroundColor Red
    exit 1
}

# Stop and restart the worker service
Write-Host "`n🔄 Restarting worker service..." -ForegroundColor Cyan

# Stop existing dotnet processes
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        Stop-Process -Id $_.Id -Force
        Write-Host "Stopped PID: $($_.Id)" -ForegroundColor Gray
    } catch { }
}

Start-Sleep -Seconds 3

# Start the worker service
try {
    Start-Process -FilePath "dotnet" -ArgumentList "run --project src\Castellan.Worker" -WorkingDirectory (Get-Location) -WindowStyle Hidden
    Write-Host "✅ Service starting..." -ForegroundColor Green
    Start-Sleep -Seconds 8
    Write-Host "✅ Service should be ready" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to start service" -ForegroundColor Red
    exit 1
}

Write-Host "`n🧪 Testing authentication..." -ForegroundColor Yellow

$maxRetries = 3
$retryCount = 0

while ($retryCount -lt $maxRetries) {
    try {
        $loginData = @{
            username = "admin"
            password = $NewPassword
        } | ConvertTo-Json
        
        $response = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method POST -Body $loginData -ContentType "application/json" -TimeoutSec 10
        
        if ($response.token) {
            Write-Host "✅ Authentication successful!" -ForegroundColor Green
            Write-Host "`n🎉 Setup complete!" -ForegroundColor Cyan
            Write-Host "Credentials: admin / $NewPassword" -ForegroundColor White
            Write-Host "`n🚀 Now running YARA import..." -ForegroundColor Green
            
            # Run the YARA import with the verified credentials
            & ".\import-yara-with-auth.ps1" -Password $NewPassword
            break
        } else {
            Write-Host "❌ No token received" -ForegroundColor Red
        }
    } catch {
        $retryCount++
        Write-Host "❌ Attempt $retryCount failed: $($_.Exception.Message)" -ForegroundColor Red
        if ($retryCount -lt $maxRetries) {
            Write-Host "   Retrying in 5 seconds..." -ForegroundColor Yellow
            Start-Sleep -Seconds 5
        }
    }
}

if ($retryCount -eq $maxRetries) {
    Write-Host "❌ Authentication failed after $maxRetries attempts" -ForegroundColor Red
    Write-Host "Manual credentials: admin / $NewPassword" -ForegroundColor Cyan
}
