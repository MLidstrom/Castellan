# Stop Castellan Applications Only
# Stops all Castellan processes but leaves Qdrant and Ollama running by default
# Compatible with Windows PowerShell 5.1 and PowerShell 7+
#
# Usage:
#   .\scripts\stop.ps1                          # Stop all Castellan apps (default)
#   .\scripts\stop.ps1 -Force                   # Force kill if graceful shutdown fails
#   .\scripts\stop.ps1 -StopQdrant              # Also stop Qdrant container
#   .\scripts\stop.ps1 -StopOllama              # Also stop Ollama service
#   .\scripts\stop.ps1 -Worker                  # Stop only Worker
#   .\scripts\stop.ps1 -ReactAdmin              # Stop only React Admin
#   .\scripts\stop.ps1 -SystemTray              # Stop only System Tray
#   .\scripts\stop.ps1 -Worker -ReactAdmin      # Stop Worker and React Admin
#   .\scripts\stop.ps1 -StopQdrant -StopOllama -Force  # Stop everything
param(
    [switch]$Force = $false,      # Force kill processes if graceful shutdown fails
    [switch]$KeepQdrant = $true,  # Default: Keep Qdrant running
    [switch]$StopQdrant = $false, # Explicit flag to stop Qdrant container
    [switch]$StopOllama = $false, # Explicit flag to stop Ollama service
    [switch]$Worker = $false,     # Stop only Worker service
    [switch]$ReactAdmin = $false, # Stop only React Admin
    [switch]$SystemTray = $false  # Stop only System Tray
)

# Ensure we're using TLS 1.2 for web requests on older PowerShell versions
if ($PSVersionTable.PSVersion.Major -lt 6) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}

# Determine which components to stop
$selectiveMode = $Worker -or $ReactAdmin -or $SystemTray
$stopWorker = -not $selectiveMode -or $Worker
$stopReactAdmin = -not $selectiveMode -or $ReactAdmin
$stopSystemTray = -not $selectiveMode -or $SystemTray

Write-Host "Stopping Castellan Applications" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
if ($selectiveMode) {
    $components = @()
    if ($stopWorker) { $components += "Worker" }
    if ($stopReactAdmin) { $components += "React Admin" }
    if ($stopSystemTray) { $components += "System Tray" }
    Write-Host "Selected components: $($components -join ', ')" -ForegroundColor Yellow
} else {
    Write-Host "Qdrant and Ollama will remain running" -ForegroundColor Gray
}
Write-Host ""

$stoppedComponents = @()
$failedComponents = @()

# Function to safely stop a process
function Stop-SafeProcess {
    param(
        [string]$ProcessName,
        [string]$DisplayName,
        [scriptblock]$ProcessFilter = { $true }
    )
    
    try {
        $processes = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Where-Object $ProcessFilter
        if ($processes) {
            foreach ($proc in $processes) {
                try {
                    if ($Force) {
                        $proc | Stop-Process -Force -ErrorAction Stop
                    } else {
                        $proc | Stop-Process -ErrorAction Stop
                    }
                    Write-Host "OK: Stopped $DisplayName (PID: $($proc.Id))" -ForegroundColor Green
                    $script:stoppedComponents += $DisplayName
                } catch {
                    Write-Host "ERROR: Failed to stop $DisplayName (PID: $($proc.Id)): $_" -ForegroundColor Red
                    $script:failedComponents += $DisplayName
                }
            }
            return $true
        } else {
            Write-Host "WARNING: $DisplayName was not running" -ForegroundColor Yellow
            return $false
        }
    } catch {
        Write-Host "WARNING: $DisplayName was not running" -ForegroundColor Yellow
        return $false
    }
}

# Stop Worker API (attempts graceful shutdown first)
if ($stopWorker) {
    Write-Host "Stopping Worker API..." -ForegroundColor Yellow
    try {
        # Try graceful shutdown via API first
        $shutdownResponse = Invoke-WebRequest -Uri "http://localhost:5000/shutdown" -Method POST -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
        if ($shutdownResponse.StatusCode -eq 200) {
            Write-Host "OK: Sent graceful shutdown signal to Worker API" -ForegroundColor Green
            Start-Sleep -Seconds 2
        }
    } catch {
        # API not responding or endpoint doesn't exist
    }
}

# Stop Worker process (using multiple detection methods)
if ($stopWorker) {
$workerStopped = $false
try {
    # Method 1: Check for processes listening on Worker API port (5000)
    $workerPort = 5000
    $portProcesses = @()
    
    try {
        $netstatOutput = & netstat -ano | Select-String ":$workerPort "
        foreach ($line in $netstatOutput) {
            if ($line -match ":$workerPort\s+.*LISTENING\s+(\d+)") {
                $pid = $matches[1]
                $process = Get-Process -Id $pid -ErrorAction SilentlyContinue
                if ($process -and $process.ProcessName -eq "dotnet") {
                    $portProcesses += $process
                }
            }
        }
    } catch {
        # Port detection failed, continue to other methods
    }
    
    # Method 2: CIM command line detection (original method)
    $cimProcesses = @()
    try {
        $cimResults = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object {
            $_.CommandLine -like "*Castellan.Worker*"
        }
        foreach ($cimProc in $cimResults) {
            $process = Get-Process -Id $cimProc.ProcessId -ErrorAction SilentlyContinue
            if ($process) {
                $cimProcesses += $process
            }
        }
    } catch {
        # CIM detection failed, continue
    }
    
    # Method 3: Check working directory for dotnet processes
    $workDirProcesses = @()
    try {
        $dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
        foreach ($proc in $dotnetProcesses) {
            try {
                $procCim = Get-CimInstance Win32_Process -Filter "ProcessId=$($proc.Id)" -ErrorAction SilentlyContinue
                if ($procCim -and $procCim.ExecutablePath -and (Split-Path $procCim.ExecutablePath -Parent) -like "*Castellan.Worker*") {
                    $workDirProcesses += $proc
                }
            } catch {
                # Continue checking other processes
            }
        }
    } catch {
        # Working directory detection failed
    }
    
    # Combine all detected processes and remove duplicates
    $allWorkerProcesses = @()
    $allWorkerProcesses += $portProcesses
    $allWorkerProcesses += $cimProcesses  
    $allWorkerProcesses += $workDirProcesses
    $uniqueWorkerProcesses = $allWorkerProcesses | Sort-Object Id | Get-Unique -AsString
    
    if ($uniqueWorkerProcesses) {
        Write-Host "Found $($uniqueWorkerProcesses.Count) Worker process(es)" -ForegroundColor Yellow
        foreach ($proc in $uniqueWorkerProcesses) {
            try {
                if ($Force) {
                    $proc | Stop-Process -Force -ErrorAction Stop
                } else {
                    $proc | Stop-Process -ErrorAction Stop
                }
                Write-Host "OK: Stopped Worker Service (PID: $($proc.Id))" -ForegroundColor Green
                $stoppedComponents += "Worker Service"
                $workerStopped = $true
            } catch {
                Write-Host "ERROR: Failed to stop Worker (PID: $($proc.Id)): $_" -ForegroundColor Red
                $failedComponents += "Worker Service"
            }
        }
    } else {
        Write-Host "WARNING: No Worker processes detected using smart detection" -ForegroundColor Yellow
        
        # Final fallback: Check all dotnet processes (only with -Force)
        $dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
        if ($dotnetProcesses -and $Force) {
            Write-Host "WARNING: Using -Force to stop all dotnet processes..." -ForegroundColor Yellow
            foreach ($proc in $dotnetProcesses) {
                try {
                    $proc | Stop-Process -Force -ErrorAction Stop
                    Write-Host "OK: Stopped dotnet process (PID: $($proc.Id))" -ForegroundColor Green
                    $stoppedComponents += "dotnet process"
                } catch {
                    Write-Host "ERROR: Failed to stop dotnet (PID: $($proc.Id))" -ForegroundColor Red
                }
            }
        } elseif ($dotnetProcesses) {
            Write-Host "INFO: Found $($dotnetProcesses.Count) dotnet process(es) but cannot confirm if they're Castellan Worker" -ForegroundColor Gray
            Write-Host "  Worker may have already stopped or is running in a different way" -ForegroundColor Gray
            Write-Host "  Use -Force flag only if you're sure you want to stop all dotnet processes" -ForegroundColor Gray
        } else {
            Write-Host "INFO: No dotnet processes found - Worker appears to be stopped" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "WARNING: Error during Worker process detection: $_" -ForegroundColor Yellow
}
}

# Stop System Tray
if ($stopSystemTray) {
    Write-Host "`nStopping System Tray..." -ForegroundColor Yellow
    Stop-SafeProcess -ProcessName "Castellan.Tray" -DisplayName "System Tray"
}

# Stop React Admin
if ($stopReactAdmin) {
    Write-Host "`nStopping React Admin..." -ForegroundColor Yellow
$nodeProcesses = Get-CimInstance Win32_Process -Filter "Name='node.exe'" -ErrorAction SilentlyContinue | Where-Object {
    $_.CommandLine -like "*castellan-admin*" -or $_.CommandLine -like "*:8080*"
}

if ($nodeProcesses) {
    foreach ($proc in $nodeProcesses) {
        try {
            Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
            Write-Host "OK: Stopped React Admin (PID: $($proc.ProcessId))" -ForegroundColor Green
            $stoppedComponents += "React Admin"
        } catch {
            Write-Host "ERROR: Failed to stop React Admin (PID: $($proc.ProcessId))" -ForegroundColor Red
            $failedComponents += "React Admin"
        }
    }
} else {
    Write-Host "WARNING: React Admin was not running" -ForegroundColor Yellow
}
}

# Stop Qdrant Docker container (only if -StopQdrant is specified)
if ($StopQdrant -and -not $KeepQdrant) {
    Write-Host "`nStopping Qdrant container..." -ForegroundColor Yellow
    try {
        $qdrantRunning = & docker ps --filter "name=qdrant" --format "{{.Names}}" 2>$null
        if ($qdrantRunning -eq "qdrant") {
            $stopResult = & docker stop qdrant --time 10 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "OK: Stopped Qdrant container" -ForegroundColor Green
                $stoppedComponents += "Qdrant"
            } else {
                Write-Host "ERROR: Failed to stop Qdrant container" -ForegroundColor Red
                $failedComponents += "Qdrant"
            }
        } else {
            Write-Host "WARNING: Qdrant container was not running" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "WARNING: Docker not available or Qdrant not running" -ForegroundColor Yellow
    }
} else {
    Write-Host "`nINFO: Keeping Qdrant running (use -StopQdrant to stop it)" -ForegroundColor Gray
}

# Stop Ollama service (only if -StopOllama is specified)
if ($StopOllama) {
    Write-Host "`nStopping Ollama service..." -ForegroundColor Yellow
    try {
        $ollamaProcesses = Get-Process -Name "ollama" -ErrorAction SilentlyContinue
        if ($ollamaProcesses) {
            foreach ($proc in $ollamaProcesses) {
                try {
                    if ($Force) {
                        $proc | Stop-Process -Force -ErrorAction Stop
                    } else {
                        $proc | Stop-Process -ErrorAction Stop
                    }
                    Write-Host "OK: Stopped Ollama service (PID: $($proc.Id))" -ForegroundColor Green
                    $stoppedComponents += "Ollama"
                } catch {
                    Write-Host "ERROR: Failed to stop Ollama (PID: $($proc.Id)): $_" -ForegroundColor Red
                    $failedComponents += "Ollama"
                }
            }
        } else {
            Write-Host "WARNING: Ollama service was not running" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "WARNING: Could not check for Ollama processes" -ForegroundColor Yellow
    }
} else {
    Write-Host "`nINFO: Keeping Ollama running (use -StopOllama to stop it)" -ForegroundColor Gray
}

# Kill any orphaned cmd windows running npm
if ($Force) {
    Write-Host "`nCleaning up orphaned processes..." -ForegroundColor Yellow
    $cmdProcesses = Get-CimInstance Win32_Process -Filter "Name='cmd.exe'" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -like "*npm*" -and $_.CommandLine -like "*castellan*"
    }
    
    if ($cmdProcesses) {
        foreach ($proc in $cmdProcesses) {
            try {
                Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
                Write-Host "OK: Stopped orphaned cmd process (PID: $($proc.ProcessId))" -ForegroundColor Green
            } catch {
                # Ignore errors for orphaned processes
            }
        }
    }
}

# Summary
Write-Host "`n==============================" -ForegroundColor Cyan
Write-Host "Shutdown Summary:" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

if ($stoppedComponents.Count -gt 0) {
    Write-Host "OK: Successfully stopped:" -ForegroundColor Green
    foreach ($component in $stoppedComponents | Select-Object -Unique) {
        Write-Host "  - $component" -ForegroundColor Green
    }
}

if ($failedComponents.Count -gt 0) {
    Write-Host "`nERROR: Failed to stop:" -ForegroundColor Red
    foreach ($component in $failedComponents | Select-Object -Unique) {
        Write-Host "  - $component" -ForegroundColor Red
    }
    Write-Host "`nTry running with -Force flag to force stop all processes" -ForegroundColor Yellow
}

if ($stoppedComponents.Count -eq 0 -and $failedComponents.Count -eq 0) {
    Write-Host "No Castellan components were running" -ForegroundColor Yellow
}

Write-Host "`nCastellan shutdown complete" -ForegroundColor Cyan
Write-Host "Run .\scripts\status.ps1 to verify all components are stopped" -ForegroundColor Gray