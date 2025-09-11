# Import YARA Rules into Castellan
# This script reads YARA rule files and imports them via the API

param(
    [string]$ApiUrl = "http://localhost:5000/api",
    [string]$RulesDirectory = "C:\temp\yara-rules"
)

Write-Host "🚀 IMPORTING YARA RULES INTO CASTELLAN" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

# Function to extract individual rules from a YARA file
function Split-YaraRules {
    param([string]$FilePath, [string]$FileName)
    
    $content = Get-Content $FilePath -Raw
    $rules = @()
    
    # Split on 'rule ' pattern but keep the rule keyword
    $ruleBlocks = $content -split '(?=rule\s+\w+)'
    
    foreach ($block in $ruleBlocks) {
        if ($block.Trim() -match '^rule\s+(\w+)') {
            $ruleName = $matches[1]
            $ruleContent = $block.Trim()
            
            # Determine threat level based on rule name patterns
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

# Function to send rule to API
function Import-YaraRule {
    param($Rule)
    
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
    
    try {
        $response = Invoke-RestMethod -Uri "$ApiUrl/yara-rules" -Method POST -Body $body -ContentType "application/json"
        Write-Host "✅ Imported: $($Rule.name)" -ForegroundColor Green
        return $true
    }
    catch {
        $errorMsg = $_.Exception.Message
        if ($_.Exception.Response) {
            try {
                $errorDetails = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($errorDetails)
                $responseBody = $reader.ReadToEnd()
                if ($responseBody) {
                    $errorMsg += " - $responseBody"
                }
            }
            catch { }
        }
        Write-Host "❌ Failed to import $($Rule.name): $errorMsg" -ForegroundColor Red
        return $false
    }
}

# Main import process
$totalRules = 0
$successCount = 0
$failCount = 0

if (Test-Path $RulesDirectory) {
    $yaraFiles = Get-ChildItem $RulesDirectory -Filter "*.yar"
    
    foreach ($file in $yaraFiles) {
        Write-Host "`n📄 Processing: $($file.Name)" -ForegroundColor Yellow
        
        $rules = Split-YaraRules -FilePath $file.FullName -FileName $file.BaseName
        $totalRules += $rules.Count
        
        Write-Host "   Found $($rules.Count) rules in $($file.Name)"
        
        foreach ($rule in $rules) {
            if (Import-YaraRule -Rule $rule) {
                $successCount++
            } else {
                $failCount++
            }
            Start-Sleep -Milliseconds 100  # Small delay to avoid overwhelming the API
        }
    }
} else {
    Write-Host "❌ Rules directory not found: $RulesDirectory" -ForegroundColor Red
    exit 1
}

Write-Host "`n📊 IMPORT SUMMARY" -ForegroundColor Cyan
Write-Host "================" -ForegroundColor Cyan
Write-Host "Total rules processed: $totalRules"
Write-Host "Successfully imported: $successCount" -ForegroundColor Green
Write-Host "Failed imports: $failCount" -ForegroundColor Red
Write-Host "Success rate: $([math]::Round(($successCount / $totalRules) * 100, 1))%"

if ($successCount -gt 0) {
    Write-Host "`n🎉 Import completed! Check your Castellan dashboard to see the new rules." -ForegroundColor Green
}
