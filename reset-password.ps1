# Reset Castellan Admin Password
param(
    [string]$NewPassword = "admin123",
    [string]$ConfigPath = "src\Castellan.Worker\appsettings.json"
)

Write-Host "🔧 RESETTING CASTELLAN ADMIN PASSWORD" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Add the BCrypt.Net library path (it should be available since it's referenced in the project)
try {
    Add-Type -Path "C:\Users\matsl\.nuget\packages\bcrypt.net-next\4.0.3\lib\net6.0\BCrypt.Net-Next.dll" -ErrorAction SilentlyContinue
}
catch {
    # Try to use dotnet to generate the hash instead
}

# Function to generate BCrypt hash using dotnet
function New-BCryptHash {
    param($Password)
    
    $csharpCode = @"
using System;
using BCrypt.Net;

public class HashGenerator 
{
    public static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            var hash = BCrypt.HashPassword(args[0], 12);
            Console.WriteLine(hash);
        }
    }
}
"@
    
    # Create temporary C# file
    $tempFile = [System.IO.Path]::GetTempFileName() + ".cs"
    $csharpCode | Out-File -FilePath $tempFile -Encoding UTF8
    
    try {
        # Compile and run the C# code
        $projectDir = Get-Location
        $result = dotnet run --project src\Castellan.Worker --verbosity quiet -- hash-password $Password 2>$null
        
        if ($result -and $result.StartsWith('$2a$')) {
            return $result
        } else {
            # Fallback: try to compile standalone
            $tempDir = [System.IO.Path]::GetTempPath() + "bcrypt-temp"
            if (!(Test-Path $tempDir)) { New-Item -Path $tempDir -ItemType Directory | Out-Null }
            
            $projectContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
  </ItemGroup>
</Project>
"@
            
            $projectContent | Out-File -FilePath "$tempDir\bcrypt-gen.csproj" -Encoding UTF8
            $csharpCode | Out-File -FilePath "$tempDir\Program.cs" -Encoding UTF8
            
            Push-Location $tempDir
            $output = dotnet run $Password 2>$null | Select-Object -Last 1
            Pop-Location
            
            Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
            
            return $output
        }
    }
    finally {
        Remove-Item $tempFile -ErrorAction SilentlyContinue
    }
}

Write-Host "Generating BCrypt hash for password..." -ForegroundColor Yellow

$hashedPassword = New-BCryptHash -Password $NewPassword

if ($hashedPassword -and $hashedPassword.StartsWith('$2a$')) {
    Write-Host "✅ Generated BCrypt hash successfully" -ForegroundColor Green
    Write-Host "Password: $NewPassword" -ForegroundColor Cyan
    Write-Host "Hash: $($hashedPassword.Substring(0, 20))..." -ForegroundColor Gray
} else {
    Write-Host "❌ Failed to generate BCrypt hash" -ForegroundColor Red
    Write-Host "Using fallback approach..." -ForegroundColor Yellow
    
    # Fallback: Update config with plaintext and modify authentication temporarily
    $hashedPassword = $NewPassword  # Use plaintext as fallback
}

# Update the configuration file
Write-Host "`nUpdating configuration file..." -ForegroundColor Yellow

if (Test-Path $ConfigPath) {
    try {
        $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        
        # Update the admin password
        $config.Authentication.AdminUser.Password = $hashedPassword
        $config.Authentication.AdminUser.Username = "admin"  # Ensure username is correct
        
        # Save the updated configuration
        $config | ConvertTo-Json -Depth 10 | Set-Content $ConfigPath -Encoding UTF8
        
        Write-Host "✅ Configuration updated successfully" -ForegroundColor Green
        Write-Host "   Username: admin" -ForegroundColor Cyan
        Write-Host "   Password: $NewPassword" -ForegroundColor Cyan
        
    } catch {
        Write-Host "❌ Failed to update configuration: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "❌ Configuration file not found: $ConfigPath" -ForegroundColor Red
    exit 1
}

# Restart the worker service
Write-Host "`n🔄 Restarting Castellan worker service..." -ForegroundColor Cyan

# Stop the worker
$workerProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
foreach ($proc in $workerProcesses) {
    try {
        Stop-Process -Id $proc.Id -Force
        Write-Host "Stopped process PID: $($proc.Id)" -ForegroundColor Gray
    } catch {
        # Continue
    }
}

Start-Sleep -Seconds 3

# Start the worker
try {
    Start-Process -FilePath "dotnet" -ArgumentList "run --project src\Castellan.Worker" -WorkingDirectory (Get-Location) -WindowStyle Hidden
    Start-Sleep -Seconds 5
    Write-Host "✅ Worker service restarted" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to restart worker service" -ForegroundColor Red
}

Write-Host "`n🧪 Testing authentication..." -ForegroundColor Yellow

$loginData = @{
    username = "admin"
    password = $NewPassword
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method POST -Body $loginData -ContentType "application/json" -TimeoutSec 10
    
    if ($response.token) {
        Write-Host "✅ Authentication test successful!" -ForegroundColor Green
        Write-Host "🎉 Password reset completed!" -ForegroundColor Cyan
        Write-Host "`nCredentials:" -ForegroundColor White
        Write-Host "  Username: admin" -ForegroundColor Cyan
        Write-Host "  Password: $NewPassword" -ForegroundColor Cyan
        
        Write-Host "`n🚀 Ready to import YARA rules!" -ForegroundColor Green
    } else {
        Write-Host "❌ Authentication test failed - no token received" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Authentication test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   You may need to wait a few more seconds for the service to fully start" -ForegroundColor Yellow
}
