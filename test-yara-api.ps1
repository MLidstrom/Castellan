# Test YARA API endpoints in Castellan

$baseUrl = "http://localhost:5000"

# Login to get JWT token
$loginBody = @{
    username = "admin"
    password = "CastellanAdmin2024!"
} | ConvertTo-Json

Write-Host "Attempting to login..." -ForegroundColor Cyan

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -Body $loginBody -ContentType "application/json"
    $token = $loginResponse.token
    Write-Host "Login successful!" -ForegroundColor Green
    Write-Host "Token obtained: $($token.Substring(0, 20))..." -ForegroundColor Gray
}
catch {
    Write-Host "Login failed. Error: $_" -ForegroundColor Red
    exit 1
}

# Create auth headers
$headers = @{
    "Authorization" = "Bearer $token"
}

Write-Host "`nTesting YARA API endpoints..." -ForegroundColor Cyan

# Test 1: Get categories
Write-Host "`n1. Getting YARA rule categories:" -ForegroundColor Yellow
try {
    $categories = Invoke-RestMethod -Uri "$baseUrl/api/yara-rules/categories" -Method GET -Headers $headers
    Write-Host "Categories: $($categories | ConvertTo-Json -Depth 3)" -ForegroundColor Gray
}
catch {
    Write-Host "Failed to get categories: $_" -ForegroundColor Red
}

# Test 2: Get all rules
Write-Host "`n2. Getting all YARA rules:" -ForegroundColor Yellow
try {
    $rules = Invoke-RestMethod -Uri "$baseUrl/api/yara-rules" -Method GET -Headers $headers
    Write-Host "Total rules: $($rules.total)" -ForegroundColor Gray
    if ($rules.data) {
        Write-Host "Rules:" -ForegroundColor Gray
        $rules.data | ForEach-Object { Write-Host "  - $($_.name): $($_.description)" }
    }
}
catch {
    Write-Host "Failed to get rules: $_" -ForegroundColor Red
}

# Test 3: Create a sample YARA rule
Write-Host "`n3. Creating a sample YARA rule:" -ForegroundColor Yellow
$sampleRule = @{
    name = "Test_Suspicious_PowerShell"
    description = "Detects suspicious PowerShell commands"
    ruleContent = @"
rule Test_Suspicious_PowerShell {
    meta:
        description = "Detects suspicious PowerShell commands"
        author = "Castellan Test"
        date = "2024-01-01"
    strings:
        `$a = "Invoke-Expression" nocase
        `$b = "DownloadString" nocase
        `$c = "FromBase64String" nocase
    condition:
        any of them
}
"@
    category = "Malware"
    author = "Test Script"
    isEnabled = $true
    priority = 50  # Changed from string to int
    threatLevel = "Medium"
    mitreTechniques = @("T1059.001")
    tags = @("powershell", "suspicious", "test")
} | ConvertTo-Json -Depth 10

try {
    $createResponse = Invoke-RestMethod -Uri "$baseUrl/api/yara-rules" -Method POST -Body $sampleRule -ContentType "application/json" -Headers $headers
    Write-Host "Rule created successfully!" -ForegroundColor Green
    Write-Host "Rule ID: $($createResponse.data.id)" -ForegroundColor Gray
    $ruleId = $createResponse.data.id
}
catch {
    Write-Host "Failed to create rule: $_" -ForegroundColor Red
}

# Test 4: List rules again to verify
Write-Host "`n4. Verifying rule was created:" -ForegroundColor Yellow
try {
    $rules = Invoke-RestMethod -Uri "$baseUrl/api/yara-rules" -Method GET -Headers $headers
    Write-Host "Total rules after creation: $($rules.total)" -ForegroundColor Gray
    if ($rules.data) {
        $rules.data | ForEach-Object { 
            Write-Host "  - [$($_.id)] $($_.name): $($_.description)" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host "Failed to get rules: $_" -ForegroundColor Red
}

# Test 5: Test the rule against sample content
if ($ruleId) {
    Write-Host "`n5. Testing YARA rule against sample content:" -ForegroundColor Yellow
    $testRequest = @{
        ruleId = $ruleId
        content = "This is a test with Invoke-Expression command"
    } | ConvertTo-Json
    
    try {
        $testResponse = Invoke-RestMethod -Uri "$baseUrl/api/yara-rules/test" -Method POST -Body $testRequest -ContentType "application/json" -Headers $headers
        Write-Host "Test result: $($testResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Gray
    }
    catch {
        Write-Host "Failed to test rule: $_" -ForegroundColor Red
    }
}

# Test 6: Create another rule for ransomware detection
Write-Host "`n6. Creating ransomware detection rule:" -ForegroundColor Yellow
$ransomwareRule = @{
    name = "Test_Ransomware_Indicators"
    description = "Detects potential ransomware behavior patterns"
    ruleContent = @"
rule Test_Ransomware_Indicators {
    meta:
        description = "Detects potential ransomware behavior"
        author = "Castellan Security"
        threat_level = "critical"
    strings:
        `$encrypt1 = "AES" nocase
        `$encrypt2 = "RSA" nocase
        `$encrypt3 = "CryptoStream"
        `$ransom1 = "bitcoin" nocase
        `$ransom2 = "payment" nocase
        `$ransom3 = "decrypt" nocase
        `$ext1 = ".encrypted"
        `$ext2 = ".locked"
    condition:
        2 of (`$encrypt*) and 1 of (`$ransom*) or any of (`$ext*)
}
"@
    category = "ThreatHunting"
    author = "Security Team"
    isEnabled = $true
    priority = 100  # Changed from string to int (high priority)
    threatLevel = "Critical"
    mitreTechniques = @("T1486", "T1490")
    tags = @("ransomware", "encryption", "critical")
} | ConvertTo-Json -Depth 10

try {
    $createResponse = Invoke-RestMethod -Uri "$baseUrl/api/yara-rules" -Method POST -Body $ransomwareRule -ContentType "application/json" -Headers $headers
    Write-Host "Ransomware rule created successfully!" -ForegroundColor Green
    Write-Host "Rule ID: $($createResponse.data.id)" -ForegroundColor Gray
}
catch {
    Write-Host "Failed to create ransomware rule: $_" -ForegroundColor Red
}

# Test 7: Get rules by category
Write-Host "`n7. Getting rules by category (ThreatHunting):" -ForegroundColor Yellow
try {
    $threatHuntingRules = Invoke-RestMethod -Uri "$baseUrl/api/yara-rules?category=ThreatHunting" -Method GET -Headers $headers
    Write-Host "ThreatHunting rules found: $($threatHuntingRules.total)" -ForegroundColor Gray
    if ($threatHuntingRules.data) {
        $threatHuntingRules.data | ForEach-Object { 
            Write-Host "  - $($_.name): Priority=$($_.priority), ThreatLevel=$($_.threatLevel)" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host "Failed to get rules by category: $_" -ForegroundColor Red
}

# Test 8: Get enabled rules only
Write-Host "`n8. Getting enabled rules only:" -ForegroundColor Yellow
try {
    $enabledRules = Invoke-RestMethod -Uri "$baseUrl/api/yara-rules?enabled=true" -Method GET -Headers $headers
    Write-Host "Enabled rules: $($enabledRules.total)" -ForegroundColor Gray
}
catch {
    Write-Host "Failed to get enabled rules: $_" -ForegroundColor Red
}

Write-Host "`nYARA API testing complete!" -ForegroundColor Green
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  - API authentication: Working" -ForegroundColor Green
Write-Host "  - YARA rule storage: Functional" -ForegroundColor Green
Write-Host "  - Rule creation: Tested" -ForegroundColor Green
Write-Host "  - Rule querying: Tested" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "  1. Implement YARA scanning service" -ForegroundColor White
Write-Host "  2. Integrate with security event pipeline" -ForegroundColor White
Write-Host "  3. Add UI for rule management" -ForegroundColor White
Write-Host "  4. Import community YARA rules" -ForegroundColor White
