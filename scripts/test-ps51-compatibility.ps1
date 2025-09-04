# Windows PowerShell 5.1 Compatibility Test
# This script verifies that all Castellan scripts are compatible with native Windows PowerShell

param(
    [switch]$Verbose = $false
)

Write-Host "Castellan Windows PowerShell 5.1 Compatibility Test" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "Testing on: $($PSVersionTable.PSEdition) $($PSVersionTable.PSVersion)" -ForegroundColor White
Write-Host ""

$testResults = @()

# Test 1: PowerShell Version Check
Write-Host "Test 1: PowerShell Version Compatibility..." -ForegroundColor Yellow
if ($PSVersionTable.PSVersion.Major -ge 5) {
    Write-Host "OK: PowerShell $($PSVersionTable.PSVersion) is supported" -ForegroundColor Green
    $testResults += @{ Test = "PowerShell Version"; Result = "PASS"; Details = "$($PSVersionTable.PSVersion)" }
} else {
    Write-Host "ERROR: PowerShell $($PSVersionTable.PSVersion) is too old. Minimum version is 5.0" -ForegroundColor Red
    $testResults += @{ Test = "PowerShell Version"; Result = "FAIL"; Details = "$($PSVersionTable.PSVersion)" }
}

# Test 2: TLS Configuration
Write-Host "`nTest 2: TLS Configuration..." -ForegroundColor Yellow
try {
    if ($PSVersionTable.PSVersion.Major -lt 6) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    }
    Write-Host "OK: TLS 1.2 configured successfully" -ForegroundColor Green
    $testResults += @{ Test = "TLS Configuration"; Result = "PASS"; Details = "TLS 1.2 enabled" }
} catch {
    Write-Host "ERROR: Failed to configure TLS 1.2: $_" -ForegroundColor Red
    $testResults += @{ Test = "TLS Configuration"; Result = "FAIL"; Details = $_.Exception.Message }
}

# Test 3: Web Request Functionality
Write-Host "`nTest 3: Web Request with -UseBasicParsing..." -ForegroundColor Yellow
try {
    $testResponse = Invoke-WebRequest -Uri "http://httpbin.org/get" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
    if ($testResponse.StatusCode -eq 200) {
        Write-Host "OK: Web requests work with -UseBasicParsing" -ForegroundColor Green
        $testResults += @{ Test = "Web Requests"; Result = "PASS"; Details = "HTTP $($testResponse.StatusCode)" }
    } else {
        throw "Unexpected status code: $($testResponse.StatusCode)"
    }
} catch {
    Write-Host "ERROR: Web request failed: $_" -ForegroundColor Red
    $testResults += @{ Test = "Web Requests"; Result = "FAIL"; Details = $_.Exception.Message }
}

# Test 4: Process Management
Write-Host "`nTest 4: Process Management..." -ForegroundColor Yellow
try {
    $processes = Get-Process -Name "explorer" -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host "OK: Process management works (found $($processes.Count) explorer process(es))" -ForegroundColor Green
        $testResults += @{ Test = "Process Management"; Result = "PASS"; Details = "Found $($processes.Count) processes" }
    } else {
        throw "No explorer processes found"
    }
} catch {
    Write-Host "ERROR: Process management failed: $_" -ForegroundColor Red
    $testResults += @{ Test = "Process Management"; Result = "FAIL"; Details = $_.Exception.Message }
}

# Test 5: CIM/WMI Support
Write-Host "`nTest 5: CIM/WMI Support..." -ForegroundColor Yellow
try {
    $osInfo = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop | Select-Object -First 1
    if ($osInfo) {
        Write-Host "OK: CIM/WMI works - OS: $($osInfo.Caption)" -ForegroundColor Green
        $testResults += @{ Test = "CIM/WMI Support"; Result = "PASS"; Details = $osInfo.Caption }
    } else {
        throw "No OS information retrieved"
    }
} catch {
    Write-Host "ERROR: CIM/WMI failed: $_" -ForegroundColor Red
    $testResults += @{ Test = "CIM/WMI Support"; Result = "FAIL"; Details = $_.Exception.Message }
}

# Test 6: Script Syntax Validation
Write-Host "`nTest 6: Script Syntax Validation..." -ForegroundColor Yellow
$scriptPath = Join-Path $PSScriptRoot ""
$scripts = @("start.ps1", "status.ps1", "stop.ps1", "run-tests.ps1")
$syntaxErrors = 0

foreach ($script in $scripts) {
    $fullPath = Join-Path $PSScriptRoot $script
    if (Test-Path $fullPath) {
        try {
            $tokens = $null
            $errors = $null
            [void][System.Management.Automation.Language.Parser]::ParseFile($fullPath, [ref]$tokens, [ref]$errors)
            
            if ($errors) {
                Write-Host "ERROR: Syntax errors in $script" -ForegroundColor Red
                if ($Verbose) {
                    foreach ($error in $errors) {
                        Write-Host "  Line $($error.Extent.StartLineNumber): $($error.Message)" -ForegroundColor Red
                    }
                }
                $syntaxErrors++
            } else {
                Write-Host "OK: $script syntax is valid" -ForegroundColor Green
            }
        } catch {
            Write-Host "ERROR: Failed to parse $script : $_" -ForegroundColor Red
            $syntaxErrors++
        }
    } else {
        Write-Host "WARNING: $script not found" -ForegroundColor Yellow
    }
}

if ($syntaxErrors -eq 0) {
    $testResults += @{ Test = "Script Syntax"; Result = "PASS"; Details = "All scripts valid" }
} else {
    $testResults += @{ Test = "Script Syntax"; Result = "FAIL"; Details = "$syntaxErrors scripts have errors" }
}

# Test Summary
Write-Host "`n===================================================" -ForegroundColor Cyan
Write-Host "Compatibility Test Results:" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

$passCount = ($testResults | Where-Object { $_.Result -eq "PASS" }).Count
$failCount = ($testResults | Where-Object { $_.Result -eq "FAIL" }).Count

foreach ($result in $testResults) {
    if ($result.Result -eq "PASS") {
        Write-Host "PASS: $($result.Test) - $($result.Details)" -ForegroundColor Green
    } else {
        Write-Host "FAIL: $($result.Test) - $($result.Details)" -ForegroundColor Red
    }
}

Write-Host "`nOverall Result:" -ForegroundColor Cyan
if ($failCount -eq 0) {
    Write-Host "SUCCESS: All tests passed ($passCount/$($testResults.Count))" -ForegroundColor Green
    Write-Host "Your system is fully compatible with Castellan PowerShell scripts!" -ForegroundColor Green
} else {
    Write-Host "PARTIAL: $passCount passed, $failCount failed out of $($testResults.Count) tests" -ForegroundColor Yellow
    Write-Host "Some features may not work correctly. Please check the failed tests above." -ForegroundColor Yellow
}

Write-Host "`nSystem Information:" -ForegroundColor Gray
Write-Host "  PowerShell Version: $($PSVersionTable.PSVersion)" -ForegroundColor Gray
Write-Host "  PowerShell Edition: $($PSVersionTable.PSEdition)" -ForegroundColor Gray
Write-Host "  OS Version: $($PSVersionTable.BuildVersion)" -ForegroundColor Gray
Write-Host "  CLR Version: $($PSVersionTable.CLRVersion)" -ForegroundColor Gray

exit $failCount
