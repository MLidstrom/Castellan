# Start Castellan - Enhanced wrapper with validation and error handling
param(
    [switch]$NoBuild = $false,
    [switch]$Background = $false
)

Write-Host "Starting Castellan Worker Service..." -ForegroundColor Cyan
Write-Host "The Worker will automatically start all required services." -ForegroundColor Gray
Write-Host ""

# Function to check if .NET is installed
function Test-DotNetInstalled {
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ .NET SDK found: $dotnetVersion" -ForegroundColor Green
            return $true
        }
    } catch {}
    
    Write-Host "✗ .NET SDK not found. Please install .NET 8.0 or later from https://dotnet.microsoft.com/download" -ForegroundColor Red
    return $false
}

# Function to validate project exists
function Test-ProjectExists {
    $projectPath = Join-Path $PSScriptRoot "..\src\Castellan.Worker\Castellan.Worker.csproj"
    if (Test-Path $projectPath) {
        Write-Host "✓ Worker project found" -ForegroundColor Green
        return $true
    }
    
    Write-Host "✗ Worker project not found at: $projectPath" -ForegroundColor Red
    return $false
}

# Function to build the project
function Build-Project {
    Write-Host "`nBuilding project..." -ForegroundColor Yellow
    try {
        Push-Location (Join-Path $PSScriptRoot "..\src\Castellan.Worker")
        & dotnet build --configuration Release
        $buildSuccess = $LASTEXITCODE -eq 0
        Pop-Location
        
        if ($buildSuccess) {
            Write-Host "✓ Build successful" -ForegroundColor Green
            return $true
        } else {
            Write-Host "✗ Build failed" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "✗ Build error: $_" -ForegroundColor Red
        Pop-Location
        return $false
    }
}

# Function to start the Worker service
function Start-Worker {
    param([bool]$RunInBackground)
    
    try {
        $workerPath = Join-Path $PSScriptRoot "..\src\Castellan.Worker"
        
        if ($RunInBackground) {
            Write-Host "`nStarting Worker in background..." -ForegroundColor Yellow
            $startInfo = New-Object System.Diagnostics.ProcessStartInfo
            $startInfo.FileName = "dotnet"
            $startInfo.Arguments = "run"
            $startInfo.WorkingDirectory = $workerPath
            $startInfo.UseShellExecute = $false
            $startInfo.CreateNoWindow = $true
            
            $process = [System.Diagnostics.Process]::Start($startInfo)
            
            if ($process) {
                Write-Host "✓ Worker started in background (PID: $($process.Id))" -ForegroundColor Green
                Write-Host "  Use '.\scripts\stop.ps1' to stop all services" -ForegroundColor Gray
                Write-Host "  Use '.\scripts\status.ps1' to check service status" -ForegroundColor Gray
                return $true
            } else {
                Write-Host "✗ Failed to start Worker in background" -ForegroundColor Red
                return $false
            }
        } else {
            Write-Host "`nStarting Worker in foreground..." -ForegroundColor Yellow
            Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
            Push-Location $workerPath
            & dotnet run
            $exitCode = $LASTEXITCODE
            Pop-Location
            
            if ($exitCode -ne 0) {
                Write-Host "✗ Worker exited with code: $exitCode" -ForegroundColor Red
                return $false
            }
            return $true
        }
    } catch {
        Write-Host "✗ Failed to start Worker: $_" -ForegroundColor Red
        return $false
    }
}

# Main execution flow
Write-Host "Performing startup checks..." -ForegroundColor Cyan

# Step 1: Validate .NET installation
if (-not (Test-DotNetInstalled)) {
    exit 1
}

# Step 2: Validate project exists
if (-not (Test-ProjectExists)) {
    exit 1
}

# Step 3: Optionally build the project
if (-not $NoBuild) {
    if (-not (Build-Project)) {
        Write-Host "`nStartup failed due to build errors" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "⚠ Skipping build (--NoBuild specified)" -ForegroundColor Yellow
}

# Step 4: Start the Worker service
if (Start-Worker -RunInBackground:$Background) {
    if (-not $Background) {
        Write-Host "`nWorker service stopped" -ForegroundColor Cyan
    }
    exit 0
} else {
    Write-Host "`nFailed to start Castellan Worker" -ForegroundColor Red
    Write-Host "Check the error messages above for details" -ForegroundColor Yellow
    exit 1
}

# Note: All service orchestration is handled by StartupOrchestratorService.cs