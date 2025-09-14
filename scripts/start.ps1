# Start All Castellan Services
# Starts Castellan apps, Qdrant vector database, and Ollama AI service if not running
# Compatible with Windows PowerShell 5.1 and PowerShell 7+
#
# Usage:
#   .\scripts\start.ps1                    # Start all services (default)
#   .\scripts\start.ps1 -NoBuild           # Skip build step
#   .\scripts\start.ps1 -Foreground        # Run Worker in foreground (for debugging)
#   .\scripts\start.ps1 -NoReactAdmin      # Skip React Admin startup
#
param(
    [switch]$NoBuild = $false,        # Skip building the project
    [switch]$Foreground = $false,      # Run Worker in foreground instead of background
    [switch]$NoReactAdmin = $false     # Skip starting React Admin UI
)

# Ensure we're using TLS 1.2 for web requests on older PowerShell versions
if ($PSVersionTable.PSVersion.Major -lt 6) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}

Write-Host "Starting All Castellan Services" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host "Will start: Qdrant, Ollama, Worker API, and React Admin UI" -ForegroundColor Gray
Write-Host ""

# Function to check if .NET is installed
function Test-DotNetInstalled {
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "OK: .NET SDK found: $dotnetVersion" -ForegroundColor Green
            return $true
        }
    }
    catch {
        # Ignore error and fall through
    }
    
    Write-Host "ERROR: .NET SDK not found. Please install .NET 8.0 or later from https://dotnet.microsoft.com/download" -ForegroundColor Red
    return $false
}

# Function to validate project exists
function Test-ProjectExists {
    $projectPath = Join-Path $PSScriptRoot "..\src\Castellan.Worker\Castellan.Worker.csproj"
    if (Test-Path $projectPath) {
        Write-Host "OK: Worker project found" -ForegroundColor Green
        return $true
    }
    
    Write-Host "ERROR: Worker project not found at: $projectPath" -ForegroundColor Red
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
            Write-Host "OK: Build successful" -ForegroundColor Green
            return $true
        }
        else {
            Write-Host "ERROR: Build failed" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "ERROR: Build error: $_" -ForegroundColor Red
        Pop-Location
        return $false
    }
}

# Function to start React Admin UI
function Start-ReactAdmin {
    Write-Host "Checking React Admin UI..." -ForegroundColor Yellow
    
    # Check if React Admin is already running (dev server or static serve)
    $nodeProcesses = Get-CimInstance Win32_Process -Filter "Name='node.exe'" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -like "*castellan-admin*" -or $_.CommandLine -like "*:8080*"
    }
    
    if ($nodeProcesses) {
        Write-Host "OK: React Admin is already running" -ForegroundColor Green
        return $true
    }
    
    # Check if castellan-admin directory exists
    $reactAdminPath = Join-Path $PSScriptRoot "..\castellan-admin"
    if (-not (Test-Path $reactAdminPath)) {
        Write-Host "WARNING: React Admin directory not found - skipping UI startup" -ForegroundColor Yellow
        return $false
    }

    # Prefer production build if present
    $buildIndexPath = Join-Path $reactAdminPath "build\index.html"
    if (Test-Path $buildIndexPath) {
        try {
            Write-Host "Starting React Admin (production build) on http://localhost:8080 ..." -ForegroundColor Yellow
            $startInfo = New-Object System.Diagnostics.ProcessStartInfo
            $startInfo.FileName = "cmd.exe"
            # Use npx to serve the static build (-s) on port 8080; --yes auto-installs 'serve' if needed
            $startInfo.Arguments = "/c npx --yes serve -s build -l 8080"
            $startInfo.WorkingDirectory = $reactAdminPath
            $startInfo.UseShellExecute = $false
            $startInfo.CreateNoWindow = $true
            $process = [System.Diagnostics.Process]::Start($startInfo)
            if ($process) {
                Write-Host "OK: React Admin (prod) starting in background (PID: $($process.Id))" -ForegroundColor Green
                return $true
            } else {
                Write-Host "WARNING: Failed to launch production server; falling back to dev server" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "WARNING: Failed to launch production server: $_" -ForegroundColor Yellow
            Write-Host "Falling back to dev server (npm start)" -ForegroundColor Yellow
        }
    }
    
    # Ensure dependencies for dev server
    $nodeModulesPath = Join-Path $reactAdminPath "node_modules"
    if (-not (Test-Path $nodeModulesPath)) {
        Write-Host "Installing React Admin dependencies..." -ForegroundColor Yellow
        try {
            Push-Location $reactAdminPath
            & npm install --silent
            $installSuccess = $LASTEXITCODE -eq 0
            Pop-Location
            if (-not $installSuccess) {
                Write-Host "WARNING: Failed to install dependencies - skipping UI startup" -ForegroundColor Yellow
                return $false
            }
        }
        catch {
            Write-Host "WARNING: Failed to install dependencies: $_" -ForegroundColor Yellow
            Pop-Location
            return $false
        }
    }
    
    # Start React Admin dev server in background
    try {
        Write-Host "Starting React Admin (dev server) on http://localhost:8080 ..." -ForegroundColor Yellow
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = "cmd.exe"
        $startInfo.Arguments = "/c npm start"
        $startInfo.WorkingDirectory = $reactAdminPath
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $false
        $startInfo.RedirectStandardError = $false
        $process = [System.Diagnostics.Process]::Start($startInfo)
        if ($process) {
            Write-Host "OK: React Admin (dev) starting in background (PID: $($process.Id))" -ForegroundColor Green
            Write-Host "  UI will be available at: http://localhost:8080" -ForegroundColor Gray
            return $true
        } else {
            Write-Host "WARNING: Failed to start React Admin dev server" -ForegroundColor Yellow
            return $false
        }
    }
    catch {
        Write-Host "WARNING: Failed to start React Admin dev server: $_" -ForegroundColor Yellow
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
                Write-Host "OK: Worker started in background (PID: $($process.Id))" -ForegroundColor Green
                Write-Host "  Use '.\scripts\stop.ps1' to stop all services" -ForegroundColor Gray
                Write-Host "  Use '.\scripts\status.ps1' to check service status" -ForegroundColor Gray
                return $true
            }
            else {
                Write-Host "ERROR: Failed to start Worker in background" -ForegroundColor Red
                return $false
            }
        }
        else {
            Write-Host "`nStarting Worker in foreground..." -ForegroundColor Yellow
            Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
            Push-Location $workerPath
            & dotnet run
            $exitCode = $LASTEXITCODE
            Pop-Location
            
            if ($exitCode -ne 0) {
                Write-Host "ERROR: Worker exited with code: $exitCode" -ForegroundColor Red
                return $false
            }
            return $true
        }
    }
    catch {
        Write-Host "ERROR: Failed to start Worker: $_" -ForegroundColor Red
        return $false
    }
}

# Main execution flow
Write-Host "Performing startup checks..." -ForegroundColor Cyan

# Step 1: Check and start prerequisites (Qdrant and Ollama)
Write-Host "`nChecking prerequisites..." -ForegroundColor Cyan

# Check Qdrant
Write-Host "Checking Qdrant vector database..." -ForegroundColor Yellow
try {
    $qdrantRunning = & docker ps --filter "name=qdrant" --format "{{.Names}}" 2>$null
    if ($qdrantRunning -eq "qdrant") {
        Write-Host "OK: Qdrant is already running" -ForegroundColor Green
    } else {
        Write-Host "Starting Qdrant container..." -ForegroundColor Yellow
        $startResult = & docker start qdrant 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "OK: Started existing Qdrant container" -ForegroundColor Green
        } else {
            Write-Host "Creating new Qdrant container..." -ForegroundColor Yellow
            $createResult = & docker run -d --name qdrant -p 6333:6333 qdrant/qdrant 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "OK: Created and started new Qdrant container" -ForegroundColor Green
            } else {
                Write-Host "WARNING: Failed to start Qdrant - vector search may not work" -ForegroundColor Yellow
            }
        }
    }
} catch {
    Write-Host "WARNING: Docker not available - Qdrant will not be started" -ForegroundColor Yellow
}

# Check Ollama
Write-Host "Checking Ollama LLM service..." -ForegroundColor Yellow
try {
    $ollamaResponse = Invoke-WebRequest -Uri "http://localhost:11434/api/tags" -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
    if ($ollamaResponse.StatusCode -eq 200) {
        Write-Host "OK: Ollama is already running" -ForegroundColor Green
    } else {
        throw "Not responding"
    }
} catch {
    Write-Host "Starting Ollama service..." -ForegroundColor Yellow
    try {
        $ollamaProcess = Start-Process -FilePath "ollama" -ArgumentList "serve" -WindowStyle Hidden -PassThru
        if ($ollamaProcess) {
            Write-Host "OK: Started Ollama service (PID: $($ollamaProcess.Id))" -ForegroundColor Green
            # Wait a moment for Ollama to initialize
            Start-Sleep -Seconds 3
        } else {
            Write-Host "WARNING: Failed to start Ollama - AI analysis may not work" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "WARNING: Ollama not installed or not accessible - AI analysis may not work" -ForegroundColor Yellow
    }
}

# Step 2: Validate .NET installation
if (-not (Test-DotNetInstalled)) {
    exit 1
}

# Step 3: Validate project exists
if (-not (Test-ProjectExists)) {
    exit 1
}

# Step 4: Optionally build the project
if (-not $NoBuild) {
    if (-not (Build-Project)) {
        Write-Host "`nStartup failed due to build errors" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "WARNING: Skipping build (--NoBuild specified)" -ForegroundColor Yellow
}

# Step 5: Start the Worker service and React Admin UI
# Background is now the default; -Foreground overrides this
$runInBackground = -not $Foreground

Write-Host "`nStarting core services..." -ForegroundColor Cyan
$workerStarted = Start-Worker -RunInBackground:$runInBackground

if ($runInBackground -and $workerStarted) {
    # Only start React Admin if Worker is running in background
    Write-Host "`nStarting web interface..." -ForegroundColor Cyan
    $uiStarted = Start-ReactAdmin
    
    if ($workerStarted -and $uiStarted) {
        Write-Host "`n✅ Castellan successfully started!" -ForegroundColor Green
        Write-Host "All services are running in the background." -ForegroundColor Gray
        Write-Host "Web UI: http://localhost:8080" -ForegroundColor Gray
    } elseif ($workerStarted) {
        Write-Host "`n✅ Castellan core services started!" -ForegroundColor Green
        Write-Host "Worker service is running. Web UI startup was skipped or failed." -ForegroundColor Yellow
    } else {
        Write-Host "`n⚠️  Castellan partially started" -ForegroundColor Yellow
        Write-Host "Worker service failed to start. Some functionality may be limited." -ForegroundColor Yellow
        exit 1
    }
    exit 0
}
elseif ($workerStarted) {
    # Worker started in foreground mode
    Write-Host "`nWorker service stopped" -ForegroundColor Cyan
    exit 0
}
else {
    Write-Host "`nFailed to start Castellan Worker" -ForegroundColor Red
    Write-Host "Check the error messages above for details" -ForegroundColor Yellow
    exit 1
}

# Note: All service orchestration is handled by StartupOrchestratorService.cs