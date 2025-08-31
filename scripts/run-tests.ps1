# Castellan Test Runner Script
# This script runs all unit tests for the Castellan project

param(
    [string]$Configuration = "Release",
    [switch]$NoBuild = $false,
    [string]$TestProject = "src\Castellan.Tests\Castellan.Tests.csproj",
    [string]$Verbosity = "normal"
)

Write-Host "Castellan Test Runner" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Test Project: $TestProject" -ForegroundColor White
Write-Host "No Build: $NoBuild" -ForegroundColor White
Write-Host "Verbosity: $Verbosity" -ForegroundColor White
Write-Host ""

# Function to check if test project exists
function Test-ProjectExists {
    param([string]$ProjectPath)
    
    if (Test-Path $ProjectPath) {
        return $true
    } else {
        Write-Host "ERROR: Test project not found at $ProjectPath" -ForegroundColor Red
        return $false
    }
}

# Function to run tests with proper error handling
function Invoke-Tests {
    param(
        [string]$ProjectPath,
        [string]$Config,
        [bool]$SkipBuild,
        [string]$VerbosityLevel
    )
    
    Write-Host "Running tests..." -ForegroundColor Yellow
    
    try {
        # Build the test command
        $testArgs = @(
            "test",
            $ProjectPath,
            "--configuration", $Config,
            "--verbosity", $VerbosityLevel
        )
        
        if ($SkipBuild) {
            $testArgs += "--no-build"
        }
        
        # Display the command being run
        Write-Host "Command: dotnet $($testArgs -join ' ')" -ForegroundColor Gray
        Write-Host ""
        
        # Run the tests
        & dotnet @testArgs
        
        return $LASTEXITCODE
    } catch {
        Write-Host "ERROR: Exception occurred during test execution - $($_.Exception.Message)" -ForegroundColor Red
        return 1
    }
}

# Main execution
Write-Host "Step 1: Validating test project..." -ForegroundColor Yellow
if (-not (Test-ProjectExists $TestProject)) {
    Write-Host "FAILED: Cannot proceed without valid test project" -ForegroundColor Red
    exit 1
}
Write-Host "SUCCESS: Test project found" -ForegroundColor Green

Write-Host "`nStep 2: Running unit tests..." -ForegroundColor Yellow
$testExitCode = Invoke-Tests -ProjectPath $TestProject -Config $Configuration -SkipBuild $NoBuild -VerbosityLevel $Verbosity

# Analyze results
Write-Host "`nTest Results:" -ForegroundColor Cyan
if ($testExitCode -eq 0) {
    Write-Host "SUCCESS: All tests passed" -ForegroundColor Green
    Write-Host "`nTest execution completed successfully!" -ForegroundColor Green
} else {
    Write-Host "FAILED: Some tests failed (exit code $testExitCode)" -ForegroundColor Red
    Write-Host "`nTroubleshooting:" -ForegroundColor Yellow
    Write-Host "   1. Check the test output above for specific failures" -ForegroundColor White
    Write-Host "   2. Ensure all dependencies are properly installed" -ForegroundColor White
    Write-Host "   3. Verify the project builds successfully before running tests" -ForegroundColor White
    Write-Host "   4. Check for any missing test data or configuration files" -ForegroundColor White
    Write-Host "   5. Run individual test methods to isolate failures" -ForegroundColor White
}

Write-Host "`nAdditional Commands:" -ForegroundColor Cyan
Write-Host "   Run specific test:     dotnet test $TestProject --filter 'TestMethodName'" -ForegroundColor White
Write-Host "   Run with coverage:     dotnet test $TestProject --collect:'XPlat Code Coverage'" -ForegroundColor White
Write-Host "   Run in watch mode:     dotnet watch test $TestProject" -ForegroundColor White
Write-Host "   List all tests:        dotnet test $TestProject --list-tests" -ForegroundColor White

exit $testExitCode