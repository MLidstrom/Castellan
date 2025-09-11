# Import YARA Rules into Castellan with Authentication
param(
    [string]$ApiUrl = "http://localhost:5000/api",
    [string]$RulesDirectory = "C:\temp\yara-rules",
    [string]$Username = "admin",
    [string]$Password = ""
)

Write-Host "🔐 AUTHENTICATING AND IMPORTING YARA RULES" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan

# If no password provided, prompt for it
if (-not $Password) {
    $SecurePassword = Read-Host -Prompt "Enter admin password" -AsSecureString
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword))
}

# Function to authenticate and get JWT token
function Get-AuthToken {
    param($ApiUrl, $Username, $Password)
    
    $loginData = @{
        username = $Username
        password = $Password
    } | ConvertTo-Json
    
    try {
        Write-Host "🔐 Authenticating with API..." -ForegroundColor Yellow
        $response = Invoke-RestMethod -Uri "$ApiUrl/auth/login" -Method POST -Body $loginData -ContentType "application/json" -TimeoutSec 10
        
        if ($response.token) {
            Write-Host "✅ Authentication successful" -ForegroundColor Green
            return $response.token
        } else {
            Write-Host "❌ Authentication failed - no token in response" -ForegroundColor Red
            return $null
        }
    }
    catch {
        Write-Host "❌ Authentication failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "   Trying alternative endpoints..." -ForegroundColor Yellow
        
        # Try alternative auth endpoints
        $altEndpoints = @("$ApiUrl/login", "$ApiUrl/authenticate", "$ApiUrl/token")
        foreach ($endpoint in $altEndpoints) {
            try {
                Write-Host "   Trying: $endpoint" -ForegroundColor Gray
                $response = Invoke-RestMethod -Uri $endpoint -Method POST -Body $loginData -ContentType "application/json" -TimeoutSec 5
                if ($response.token) {
                    Write-Host "✅ Authentication successful at $endpoint" -ForegroundColor Green
                    return $response.token
                }
            }
            catch {
                # Continue to next endpoint
            }
        }
        return $null
    }
}

# Function to extract individual rules from a YARA file
function Split-YaraRules {
    param([string]$FilePath, [string]$FileName)
    
    $content = Get-Content $FilePath -Raw
    $rules = @()
    
    # Split on 'rule ' pattern but keep the rule keyword
    $ruleBlocks = $content -split '(?=rule\s+\w+)'
    
    foreach ($block in $ruleBlocks) {
        if ($block.Trim() -match '^rule\s+(\w+)' -and $block.Trim().Length -gt 20) {
            $ruleName = $matches[1]
            $ruleContent = $block.Trim()
            
            # Skip metadata-only blocks (like License blocks)
            if ($ruleContent -notmatch '\{[^}]*condition\s*:' -or $ruleName -match '^(License|Set)$') {
                continue
            }
            
            # Determine threat level
            $threatLevel = "medium"
            if ($ruleName -match "(APT|HKTL|Trojan|Malware|Backdoor)" -or $FileName -match "cobalt|apt") {
                $threatLevel = "high"
            } elseif ($ruleName -match "(Susp|Suspicious|Gen_)" -or $FileName -match "susp") {
                $threatLevel = "low"
            }
            
            # Determine category
            $category = "malware"
            if ($FileName -match "powershell") {
                $category = "script"
            } elseif ($FileName -match "apt|cobalt") {
                $category = "apt"
            }
            
            $rules += @{
                name = $ruleName
                content = $ruleContent
                description = "Imported from $FileName - $ruleName detection rule"
                category = $category
                threat_level = $threatLevel
                enabled = $true
                author = "Community/Security Researchers"
                tags = @($category, $threatLevel, "imported")
            }
        }
    }
    
    return $rules
}

# Function to send rule to API with authentication
function Import-YaraRule {
    param($Rule, $Token, $ApiUrl)
    
    $body = @{
        name = $Rule.name
        content = $Rule.content
        description = $Rule.description
        category = $Rule.category
        threat_level = $Rule.threat_level
        enabled = $Rule.enabled
        author = $Rule.author
        tags = $Rule.tags -join ","
    } | ConvertTo-Json -Depth 10
    
    $headers = @{
        "Authorization" = "Bearer $Token"
        "Content-Type" = "application/json"
    }
    
    try {
        $response = Invoke-RestMethod -Uri "$ApiUrl/yara-rules" -Method POST -Body $body -Headers $headers -TimeoutSec 10
        Write-Host "✅ Imported: $($Rule.name)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "❌ Failed to import $($Rule.name): $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Main process
Write-Host "`n1. Authenticating..."
$token = Get-AuthToken -ApiUrl $ApiUrl -Username $Username -Password $Password

if (-not $token) {
    Write-Host "❌ Could not authenticate. Please check credentials and API availability." -ForegroundColor Red
    Write-Host "   Make sure Castellan Worker is running on port 5000" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n2. Starting YARA rules import..."
$totalRules = 0
$successCount = 0
$failCount = 0

if (Test-Path $RulesDirectory) {
    $yaraFiles = Get-ChildItem $RulesDirectory -Filter "*.yar"
    
    foreach ($file in $yaraFiles) {
        Write-Host "`n📄 Processing: $($file.Name)" -ForegroundColor Yellow
        
        $rules = Split-YaraRules -FilePath $file.FullName -FileName $file.BaseName
        $totalRules += $rules.Count
        
        Write-Host "   Found $($rules.Count) valid rules in $($file.Name)"
        
        foreach ($rule in $rules) {
            if (Import-YaraRule -Rule $rule -Token $token -ApiUrl $ApiUrl) {
                $successCount++
            } else {
                $failCount++
            }
            Start-Sleep -Milliseconds 200  # Small delay to avoid overwhelming the API
        }
    }
} else {
    Write-Host "❌ Rules directory not found: $RulesDirectory" -ForegroundColor Red
    exit 1
}

Write-Host "`n📊 IMPORT SUMMARY" -ForegroundColor Cyan
Write-Host "=================" -ForegroundColor Cyan
Write-Host "Total rules processed: $totalRules"
Write-Host "Successfully imported: $successCount" -ForegroundColor Green
Write-Host "Failed imports: $failCount" -ForegroundColor Red

if ($totalRules -gt 0) {
    $successRate = [math]::Round(($successCount / $totalRules) * 100, 1)
    Write-Host "Success rate: $successRate%"
}

if ($successCount -gt 0) {
    Write-Host "`n🎉 Import completed successfully!" -ForegroundColor Green
    Write-Host "   Check your Castellan dashboard at http://localhost:8080" -ForegroundColor Cyan
}
