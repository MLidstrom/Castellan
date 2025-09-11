# YARA Rules Sources and Management Guide

## Overview

YARA rules are pattern-matching rules used to identify malware, suspicious behavior, and security threats. This guide covers where to get YARA rules and how to manage them in Castellan.

## Current Rules in Castellan

Your Castellan instance currently has **2 test rules**:

1. **Test_Suspicious_PowerShell** (Malware category)
   - Detects suspicious PowerShell commands
   - Patterns: Invoke-Expression, DownloadString, FromBase64String

2. **Test_Ransomware_Indicators** (ThreatHunting category)
   - Detects potential ransomware behavior
   - Patterns: AES/RSA encryption + payment/bitcoin keywords

## Where to Get YARA Rules

### 1. **Free Community Sources**

#### GitHub Repositories (Most Popular)
```bash
# Clone popular YARA rule repositories
git clone https://github.com/Yara-Rules/rules.git
git clone https://github.com/Neo23x0/signature-base.git
git clone https://github.com/YARAHQ/yara-rules.git
git clone https://github.com/reversinglabs/reversinglabs-yara-rules.git
```

**Key Community Repositories:**
- **Yara-Rules/rules** - Community-maintained collection
- **Neo23x0/signature-base** - Florian Roth's signature collection
- **YARAHQ/yara-rules** - Curated by YARA HQ
- **reversinglabs/reversinglabs-yara-rules** - Professional-grade rules
- **elastic/protections-artifacts** - Elastic Security rules
- **facebook/osquery** - Facebook's security rules

#### Threat Intelligence Feeds
- **AlienVault OTX** - Open Threat Exchange
- **MISP** - Malware Information Sharing Platform
- **Hybrid Analysis** - Crowd-sourced analysis
- **VirusTotal** - Community submissions

### 2. **Commercial Sources**

- **CrowdStrike Intelligence** - Premium threat intelligence
- **FireEye/Mandiant** - APT and targeted attack rules
- **Kaspersky** - Enterprise-grade rules
- **Symantec** - DeepSight Intelligence
- **Proofpoint** - Email and web threat rules

### 3. **Government/Official Sources**

- **CISA** (Cybersecurity & Infrastructure Security Agency)
- **NIST** - National Institute of Standards and Technology  
- **CERT** organizations worldwide
- **MITRE ATT&CK** framework indicators

### 4. **Industry-Specific Rules**

- **Financial Services** - Banking trojans, fraud detection
- **Healthcare** - HIPAA compliance, medical device security
- **Industrial Control Systems** - ICS/SCADA specific threats
- **Cloud Security** - AWS, Azure, GCP specific patterns

## How to Add Rules to Castellan

### Method 1: Via React Admin Dashboard (Recommended)

1. **Access Dashboard:**
   - Go to http://localhost:8080
   - Login with: admin / CastellanAdmin2024!

2. **Navigate to YARA Rules:**
   - Click on "YARA Rules" in the menu (or go to #/yara-rules)

3. **Add New Rule:**
   - Click "Create" button
   - Fill in the form with rule details
   - Paste YARA rule content
   - Set category, threat level, tags
   - Save the rule

### Method 2: Via API (Programmatic)

```powershell
# Example: Add a new YARA rule via API
$newRule = @{
    name = "Detect_Cobalt_Strike"
    description = "Detects Cobalt Strike beacon patterns"
    ruleContent = @"
rule Detect_Cobalt_Strike {
    meta:
        description = "Detects Cobalt Strike beacon"
        author = "Security Team"
        date = "2025-09-11"
        threat_level = "high"
    
    strings:
        `$beacon1 = "IEX (New-Object Net.WebClient).DownloadString"
        `$beacon2 = "powershell.exe -nop -w hidden -c"
        `$beacon3 = { 4d 5a 90 00 03 00 00 00 04 00 00 00 ff ff 00 00 }
    
    condition:
        any of them
}
"@
    category = "APT"
    threatLevel = "High"
    tags = @("cobalt-strike", "apt", "beacon")
    mitreTechniques = @("T1059.001", "T1105")
    isEnabled = $true
    priority = 90
} | ConvertTo-Json

$headers = @{ Authorization = "Bearer $token" }
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/yara-rules" -Method POST -Body $newRule -ContentType "application/json" -Headers $headers
```

### Method 3: Bulk Import

```powershell
# Import multiple rules from a file
$yaraContent = Get-Content "rules.yar" -Raw
$importData = @{
    content = $yaraContent
    skipDuplicates = $true
} | ConvertTo-Json

$importResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/yara-rules/import" -Method POST -Body $importData -ContentType "application/json" -Headers $headers
```

## Rule Categories in Castellan

### Standard Categories
- **Malware** - General malware detection
- **Ransomware** - Ransomware-specific patterns
- **Trojan** - Trojan horses and backdoors  
- **APT** - Advanced Persistent Threats
- **Backdoor** - Backdoor access tools
- **Suspicious** - Suspicious behaviors/patterns
- **PUA** - Potentially Unwanted Applications
- **Exploit** - Exploit kits and payloads
- **ThreatHunting** - Proactive threat hunting
- **Custom** - Organization-specific rules

### Threat Levels
- **Critical** - Immediate response required
- **High** - High priority investigation
- **Medium** - Standard monitoring
- **Low** - Informational/logging

## Best Practices for YARA Rules

### 1. **Rule Organization**
```
rules/
├── malware/
│   ├── trojans/
│   ├── ransomware/
│   └── apt/
├── suspicious/
│   ├── powershell/
│   ├── javascript/
│   └── network/
└── custom/
    ├── organizational/
    └── industry/
```

### 2. **Rule Naming Convention**
```
[Category]_[ThreatFamily]_[Variant]_[Date]
Examples:
- APT_CobaltStrike_Beacon_20250911
- Malware_Emotet_Banking_20250911  
- Suspicious_PowerShell_Obfuscation_20250911
```

### 3. **Rule Testing**
- Test rules against known samples
- Validate false positive rates
- Performance test with large datasets
- Regular rule effectiveness reviews

### 4. **Rule Maintenance**
- Regular updates from sources
- Performance monitoring
- False positive analysis
- Disable outdated rules

## Downloading Popular Rule Sets

Let me help you download some popular rule sets:

```powershell
# Create rules directory
New-Item -Path "C:\Users\matsl\Castellan\yara-rules" -ItemType Directory -Force

# Download popular rule sets
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/APT_APT1.yar" -OutFile "C:\Users\matsl\Castellan\yara-rules\APT1.yar"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/Neo23x0/signature-base/master/yara/apt_cobalt_strike.yar" -OutFile "C:\Users\matsl\Castellan\yara-rules\cobalt_strike.yar"
```

## Rule Performance Optimization

### High-Performance Rule Writing
- Use specific byte patterns when possible
- Avoid overly broad string matches
- Use condition logic to reduce false positives
- Implement timeout limits for complex rules

### Example: Optimized Rule
```yara
rule Optimized_Malware_Detection {
    meta:
        description = "Optimized malware detection"
        author = "Security Team"
        performance = "high"
    
    strings:
        $mz = { 4D 5A }  // PE header
        $payload1 = { E8 ?? ?? ?? ?? 5D 81 ED ?? ?? ?? ?? } // Common shellcode pattern
        $payload2 = "kernel32.dll" nocase
        $payload3 = "VirtualAlloc" nocase
    
    condition:
        $mz at 0 and 
        filesize < 10MB and
        2 of ($payload*)
}
```

## Castellan-Specific Features

### Rule Metadata
Castellan supports enhanced metadata:
- **MITRE ATT&CK** technique mapping
- **Performance metrics** tracking
- **False positive** rate monitoring
- **Hit count** statistics
- **Execution time** tracking

### Integration Features
- **Automatic scanning** of security events
- **Real-time compilation** and validation
- **Performance monitoring** dashboard
- **Rule effectiveness** analytics
- **Bulk operations** (enable/disable/delete)

## Security Considerations

### Rule Validation
- All rules are syntax-validated before activation
- Performance testing prevents resource exhaustion
- Sandboxed compilation environment
- Rule rollback capabilities

### Access Control
- JWT authentication required for all operations
- Role-based access control
- Audit logging of rule changes
- Version control integration

## Recommended Starting Rule Set

For a new Castellan deployment, I recommend starting with:

1. **Neo23x0/signature-base** - Comprehensive malware detection
2. **Elastic protections** - Modern threat patterns  
3. **YARA-Rules community** - Well-tested rules
4. **Custom organizational rules** - Tailored to your environment

Would you like me to help you download and import some of these popular rule sets into your Castellan instance?

## Quick Start Commands

```powershell
# 1. Download popular rules
git clone https://github.com/Neo23x0/signature-base.git C:\temp\signature-base

# 2. Import into Castellan (via API)
$yaraFiles = Get-ChildItem "C:\temp\signature-base\yara\*.yar"
foreach ($file in $yaraFiles[0..5]) {  # Import first 5 files as test
    $content = Get-Content $file.FullName -Raw
    $importData = @{ content = $content; skipDuplicates = $true } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5000/api/yara-rules/import" -Method POST -Body $importData -ContentType "application/json" -Headers $headers
}
```

This will give you a solid foundation of YARA rules for threat detection in your Castellan system!
