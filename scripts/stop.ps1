# Stop Castellan Components
param(
    [switch]$Force = $false,
    [switch]$KeepQdrant = $false
)

Write-Host "Stopping Castellan Components" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
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
                    Write-Host "✓ Stopped $DisplayName (PID: $($proc.Id))" -ForegroundColor Green
                    $script:stoppedComponents += $DisplayName
                } catch {
                    Write-Host "✗ Failed to stop $DisplayName (PID: $($proc.Id)): $_" -ForegroundColor Red
                    $script:failedComponents += $DisplayName
                }
            }
            return $true
        } else {
            Write-Host "⚠ $DisplayName was not running" -ForegroundColor Yellow
            return $false
        }
    } catch {
        Write-Host "⚠ $DisplayName was not running" -ForegroundColor Yellow
        return $false
    }
}

# Stop Worker API (attempts graceful shutdown first)
Write-Host "Stopping Worker API..." -ForegroundColor Yellow
try {
    # Try graceful shutdown via API first
    $shutdownResponse = Invoke-WebRequest -Uri "http://localhost:5000/shutdown" -Method POST -UseBasicParsing -ErrorAction SilentlyContinue -TimeoutSec 2
    if ($shutdownResponse.StatusCode -eq 200) {
        Write-Host "✓ Sent graceful shutdown signal to Worker API" -ForegroundColor Green
        Start-Sleep -Seconds 2
    }
} catch {
    # API not responding or endpoint doesn't exist
}

# Stop Worker process (using WMI to check command line)
$workerStopped = $false
try {
    $workerProcesses = Get-WmiObject Win32_Process -Filter "Name='dotnet.exe'" | Where-Object {
        $_.CommandLine -like "*Castellan.Worker*"
    }
    
    if ($workerProcesses) {
        foreach ($proc in $workerProcesses) {
            try {
                $process = Get-Process -Id $proc.ProcessId -ErrorAction SilentlyContinue
                if ($process) {
                    if ($Force) {
                        Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
                    } else {
                        Stop-Process -Id $proc.ProcessId -ErrorAction Stop
                    }
                    Write-Host "✓ Stopped Worker Service (PID: $($proc.ProcessId))" -ForegroundColor Green
                    $stoppedComponents += "Worker Service"
                    $workerStopped = $true
                }
            } catch {
                Write-Host "✗ Failed to stop Worker (PID: $($proc.ProcessId)): $_" -ForegroundColor Red
                $failedComponents += "Worker Service"
            }
        }
    }
    
    if (-not $workerStopped) {
        # Fallback: Stop any dotnet process (be careful)
        $dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
        if ($dotnetProcesses -and $Force) {
            Write-Host "⚠ Force stopping all dotnet processes..." -ForegroundColor Yellow
            foreach ($proc in $dotnetProcesses) {
                try {
                    $proc | Stop-Process -Force -ErrorAction Stop
                    Write-Host "✓ Stopped dotnet process (PID: $($proc.Id))" -ForegroundColor Green
                    $stoppedComponents += "dotnet process"
                } catch {
                    Write-Host "✗ Failed to stop dotnet (PID: $($proc.Id))" -ForegroundColor Red
                }
            }
        } elseif ($dotnetProcesses) {
            Write-Host "⚠ dotnet processes found but cannot confirm if they're Castellan" -ForegroundColor Yellow
            Write-Host "  Use -Force flag to stop all dotnet processes" -ForegroundColor Gray
        } else {
            Write-Host "⚠ Worker Service was not running" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "⚠ Could not check for Worker processes" -ForegroundColor Yellow
}

# Stop System Tray
Write-Host "`nStopping System Tray..." -ForegroundColor Yellow
Stop-SafeProcess -ProcessName "Castellan.Tray" -DisplayName "System Tray"

# Stop React Admin
Write-Host "`nStopping React Admin..." -ForegroundColor Yellow
$nodeProcesses = Get-WmiObject Win32_Process -Filter "Name='node.exe'" -ErrorAction SilentlyContinue | Where-Object {
    $_.CommandLine -like "*castellan-admin*" -or $_.CommandLine -like "*:8080*"
}

if ($nodeProcesses) {
    foreach ($proc in $nodeProcesses) {
        try {
            Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
            Write-Host "✓ Stopped React Admin (PID: $($proc.ProcessId))" -ForegroundColor Green
            $stoppedComponents += "React Admin"
        } catch {
            Write-Host "✗ Failed to stop React Admin (PID: $($proc.ProcessId))" -ForegroundColor Red
            $failedComponents += "React Admin"
        }
    }
} else {
    Write-Host "⚠ React Admin was not running" -ForegroundColor Yellow
}

# Stop Qdrant Docker container (unless -KeepQdrant is specified)
if (-not $KeepQdrant) {
    Write-Host "`nStopping Qdrant container..." -ForegroundColor Yellow
    try {
        $qdrantRunning = & docker ps --filter "name=qdrant" --format "{{.Names}}" 2>$null
        if ($qdrantRunning -eq "qdrant") {
            $stopResult = & docker stop qdrant --time 10 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Stopped Qdrant container" -ForegroundColor Green
                $stoppedComponents += "Qdrant"
            } else {
                Write-Host "✗ Failed to stop Qdrant container" -ForegroundColor Red
                $failedComponents += "Qdrant"
            }
        } else {
            Write-Host "⚠ Qdrant container was not running" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "⚠ Docker not available or Qdrant not running" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n⚠ Keeping Qdrant running (-KeepQdrant specified)" -ForegroundColor Yellow
}

# Kill any orphaned cmd windows running npm
if ($Force) {
    Write-Host "`nCleaning up orphaned processes..." -ForegroundColor Yellow
    $cmdProcesses = Get-WmiObject Win32_Process -Filter "Name='cmd.exe'" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -like "*npm*" -and $_.CommandLine -like "*castellan*"
    }
    
    if ($cmdProcesses) {
        foreach ($proc in $cmdProcesses) {
            try {
                Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
                Write-Host "✓ Stopped orphaned cmd process (PID: $($proc.ProcessId))" -ForegroundColor Green
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
    Write-Host "✓ Successfully stopped:" -ForegroundColor Green
    foreach ($component in $stoppedComponents | Select-Object -Unique) {
        Write-Host "  - $component" -ForegroundColor Green
    }
}

if ($failedComponents.Count -gt 0) {
    Write-Host "`n✗ Failed to stop:" -ForegroundColor Red
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