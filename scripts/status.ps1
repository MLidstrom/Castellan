# Castellan Status Check Script
# Compatible with Windows PowerShell 5.1 and PowerShell 7+
param(
    [switch]$Pause = $false,
    [switch]$Detailed = $false
)

# Ensure we're using TLS 1.2 for web requests on older PowerShell versions
if ($PSVersionTable.PSVersion.Major -lt 6) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}

Write-Host "Castellan Component Status Check" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host ""

$statusSummary = @{
    Worker = $false
    Qdrant = $false
    Ollama = $false
    ReactAdmin = $false
    SystemTray = $false
}

# Check Castellan Worker API
Write-Host "Checking Castellan Worker API..." -ForegroundColor Yellow
try {
    $workerResponse = Invoke-WebRequest -Uri "http://localhost:5000/api/analytics/trends" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
    if ($workerResponse.StatusCode -eq 200) {
        Write-Host "OK: Worker API is running on localhost:5000" -ForegroundColor Green
        $statusSummary.Worker = $true

        if ($Detailed) {
            Write-Host "  API Response: Analytics endpoint accessible" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "ERROR: Worker API is not accessible (port 5000)" -ForegroundColor Red
    Write-Host "  Run: .\scripts\start.ps1" -ForegroundColor Yellow
}

# Check Qdrant Vector Database
Write-Host "`nChecking Qdrant Vector Database..." -ForegroundColor Yellow
try {
    $qdrantResponse = Invoke-WebRequest -Uri "http://localhost:6333/collections" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
    if ($qdrantResponse.StatusCode -eq 200) {
        Write-Host "OK: Qdrant is running on localhost:6333" -ForegroundColor Green
        $statusSummary.Qdrant = $true
        
        $collections = $qdrantResponse.Content | ConvertFrom-Json
        if ($collections.result.collections) {
            Write-Host "  Collections: $($collections.result.collections.Count)" -ForegroundColor Gray
            if ($Detailed) {
                foreach ($collection in $collections.result.collections) {
                    Write-Host "    - $($collection.name)" -ForegroundColor Gray
                }
            }
        }
    }
} catch {
    Write-Host "ERROR: Qdrant is not accessible (port 6333)" -ForegroundColor Red
    Write-Host "  Run: docker run -d --name qdrant -p 6333:6333 qdrant/qdrant" -ForegroundColor Yellow
}

# Check Ollama LLM Service
Write-Host "`nChecking Ollama LLM Service..." -ForegroundColor Yellow
try {
    $ollamaResponse = Invoke-WebRequest -Uri "http://localhost:11434/api/tags" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
    if ($ollamaResponse.StatusCode -eq 200) {
        Write-Host "OK: Ollama is running on localhost:11434" -ForegroundColor Green
        $statusSummary.Ollama = $true
        
        $models = $ollamaResponse.Content | ConvertFrom-Json
        Write-Host "  Models: $($models.models.Count)" -ForegroundColor Gray
        if ($Detailed -and $models.models) {
            foreach ($model in $models.models | Select-Object -First 5) {
                $sizeGB = [math]::Round($model.size / 1GB, 2)
                Write-Host "    - $($model.name) (${sizeGB}GB)" -ForegroundColor Gray
            }
        }
    }
} catch {
    Write-Host "ERROR: Ollama is not accessible (port 11434)" -ForegroundColor Red
    Write-Host "  Install from: https://ollama.ai" -ForegroundColor Yellow
}

# Check Tailwind Dashboard
Write-Host "`nChecking Tailwind Dashboard..." -ForegroundColor Yellow
try {
    $adminResponse = Invoke-WebRequest -Uri "http://localhost:3000" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
    if ($adminResponse.StatusCode -eq 200) {
        Write-Host "OK: Tailwind Dashboard is running on localhost:3000" -ForegroundColor Green
        $statusSummary.ReactAdmin = $true
    }
} catch {
    Write-Host "ERROR: Tailwind Dashboard is not accessible (port 3000)" -ForegroundColor Red
    Write-Host "  Run: cd dashboard && npm run dev" -ForegroundColor Yellow
}

# Check System Tray Application
Write-Host "`nChecking System Tray Application..." -ForegroundColor Yellow
$trayProcess = Get-Process "Castellan.Tray" -ErrorAction SilentlyContinue
if ($trayProcess) {
    Write-Host "OK: System Tray is running (PID: $($trayProcess.Id))" -ForegroundColor Green
    $statusSummary.SystemTray = $true
} else {
    Write-Host "OK: System Tray is not running (auto-starts when needed)" -ForegroundColor Green
    Write-Host "  System Tray auto-starts when configured in appsettings.json" -ForegroundColor Gray
    $statusSummary.SystemTray = $true  # Consider this OK since it's optional
}

# Check Worker Process Details
Write-Host "`nChecking Worker Process..." -ForegroundColor Yellow

# Since we already confirmed the Worker API is responding, we know the process is running
# Just provide helpful process information
if ($statusSummary.Worker) {
    # Worker API is responding, so we know the process is running
    $dotnetProcesses = Get-Process "dotnet" -ErrorAction SilentlyContinue
    if ($dotnetProcesses) {
        Write-Host "OK: Worker Process confirmed (API responding on port 5000)" -ForegroundColor Green
        if ($Detailed) {
            Write-Host "  Found $($dotnetProcesses.Count) .NET process(es):" -ForegroundColor Gray
            foreach ($proc in $dotnetProcesses | Sort-Object WorkingSet64 -Descending | Select-Object -First 3) {
                Write-Host "    PID: $($proc.Id), Memory: $([math]::Round($proc.WorkingSet64/1MB))MB" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "OK: Worker Process running (API confirmed, process details unavailable)" -ForegroundColor Green
    }
} else {
    Write-Host "ERROR: Worker Process not detected (API not responding)" -ForegroundColor Red
}

# Check Docker Status
Write-Host "`nChecking Docker..." -ForegroundColor Yellow
try {
    $dockerVersion = & docker --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK: Docker is installed: $dockerVersion" -ForegroundColor Green
        
        # Check Qdrant container
        $qdrantContainer = & docker ps --filter "name=qdrant" --format "{{.Names}}" 2>&1
        if ($qdrantContainer -eq "qdrant") {
            Write-Host "  OK: Qdrant container is running" -ForegroundColor Green
        } else {
            Write-Host "  WARNING: Qdrant container not running" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "ERROR: Docker is not installed or not running" -ForegroundColor Red
}

# Check Log Files
Write-Host "`nChecking Log Files..." -ForegroundColor Yellow
$logPaths = @(
    "$env:LOCALAPPDATA\Castellan\logs",
    "logs",
    "src\Castellan.Worker\logs"
)

$foundLogs = $false
foreach ($logPath in $logPaths) {
    if (Test-Path $logPath) {
        $logFiles = Get-ChildItem $logPath -Filter "*.log" -ErrorAction SilentlyContinue | 
                    Sort-Object LastWriteTime -Descending | 
                    Select-Object -First 1
        
        if ($logFiles) {
            $foundLogs = $true
            Write-Host "OK: Found logs in: $logPath" -ForegroundColor Green
            
            if ($Detailed) {
                $recentLog = $logFiles[0]
                Write-Host "  Latest: $($recentLog.Name) ($('{0:N2}' -f ($recentLog.Length/1KB))KB)" -ForegroundColor Gray
                
                # Show last few lines
                $tail = Get-Content $recentLog.FullName -Tail 3 -ErrorAction SilentlyContinue
                foreach ($line in $tail) {
                    if ($line -match "ERROR|FATAL") {
                        Write-Host "    ERROR: $line" -ForegroundColor Red
                    } elseif ($line -match "WARN") {
                        Write-Host "    WARNING: $line" -ForegroundColor Yellow
                    } else {
                        Write-Host "    $($line.Substring(0, [Math]::Min(80, $line.Length)))" -ForegroundColor Gray
                    }
                }
            }
            break
        }
    }
}

if (-not $foundLogs) {
    Write-Host "WARNING: No log files found" -ForegroundColor Yellow
}

# Status Summary
Write-Host "`n=================================" -ForegroundColor Cyan
Write-Host "Status Summary:" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

$runningCount = ($statusSummary.Values | Where-Object { $_ -eq $true }).Count
$totalCount = $statusSummary.Count

if ($runningCount -eq $totalCount) {
    Write-Host "OK: All components are running ($runningCount/$totalCount)" -ForegroundColor Green
} elseif ($runningCount -gt 0) {
    Write-Host "WARNING: Partial system running ($runningCount/$totalCount)" -ForegroundColor Yellow
} else {
    Write-Host "ERROR: No components are running" -ForegroundColor Red
}

Write-Host ""
foreach ($component in $statusSummary.GetEnumerator()) {
    if ($component.Value) {
        Write-Host "  OK: $($component.Key)" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: $($component.Key)" -ForegroundColor Red
    }
}

# Quick Start Instructions
if ($runningCount -lt $totalCount) {
    Write-Host "`nQuick Start:" -ForegroundColor Yellow
    Write-Host "  .\scripts\start.ps1" -ForegroundColor White
    Write-Host "  This will start all required components" -ForegroundColor Gray
}

Write-Host "`nFor detailed status, run: .\scripts\status.ps1 -Detailed" -ForegroundColor Gray

# Wait for user input before closing (only if -Pause is specified)
if ($Pause) {
    Write-Host "`nPress any key to close this window..." -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
