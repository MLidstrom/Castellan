# Simple YARA Rules Import Script
param(
    [string]$ApiUrl = "http://localhost:5000/api",
    [string]$RulesDirectory = "C:\temp\yara-rules"
)

Write-Host "Importing YARA Rules into Castellan"
Write-Host "==================================="

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
        Write-Host "SUCCESS: Imported $($Rule.name)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "FAILED: $($Rule.name) - $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Main process
$totalRules = 0
$successCount = 0
$failCount = 0

if (Test-Path $RulesDirectory) {
    $yaraFiles = Get-ChildItem $RulesDirectory -Filter "*.yar"
    
    foreach ($file in $yaraFiles) {
        Write-Host "`nProcessing: $($file.Name)" -ForegroundColor Yellow
        
        $rules = Split-YaraRules -FilePath $file.FullName -FileName $file.BaseName
        $totalRules += $rules.Count
        
        Write-Host "Found $($rules.Count) rules in $($file.Name)"
        
        foreach ($rule in $rules) {
            if (Import-YaraRule -Rule $rule) {
                $successCount++
            } else {
                $failCount++
            }
            Start-Sleep -Milliseconds 100
        }
    }
} else {
    Write-Host "ERROR: Rules directory not found: $RulesDirectory" -ForegroundColor Red
    exit 1
}

Write-Host "`nIMPORT SUMMARY" -ForegroundColor Cyan
Write-Host "Total rules processed: $totalRules"
Write-Host "Successfully imported: $successCount" -ForegroundColor Green
Write-Host "Failed imports: $failCount" -ForegroundColor Red

if ($totalRules -gt 0) {
    $successRate = [math]::Round(($successCount / $totalRules) * 100, 1)
    Write-Host "Success rate: $successRate%"
}

if ($successCount -gt 0) {
    Write-Host "`nImport completed! Check your Castellan dashboard." -ForegroundColor Green
}
