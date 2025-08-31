# Windows Logging Hardening Guide

## 🎯 Overview

This guide provides step-by-step instructions for enabling comprehensive Windows audit policies to maximize Castellan's security detection capabilities. Proper audit policy configuration is essential for detecting security threats and maintaining compliance.

## 🔧 Prerequisites

- **Administrative Privileges**: All commands require administrative access
- **Windows 10/11**: Tested on Windows 10 and Windows 11
- **PowerShell**: Run PowerShell as Administrator

## 📋 Audit Policy Configuration

### 1. Basic Audit Policy Setup

Enable fundamental audit categories for security monitoring:

```powershell
# Enable Logon/Logoff auditing
auditpol /set /category:"Logon" /success:enable /failure:enable

# Enable Account Management auditing
auditpol /set /category:"Account Management" /success:enable /failure:enable

# Enable Policy Change auditing
auditpol /set /category:"Policy Change" /success:enable /failure:enable

# Enable Privilege Use auditing
auditpol /set /category:"Privilege Use" /success:enable /failure:enable

# Enable Object Access auditing (optional, for file/folder monitoring)
auditpol /set /category:"Object Access" /success:enable /failure:enable
```

### 2. Advanced Audit Policy Configuration

For enhanced security monitoring, configure detailed audit policies:

```powershell
# Enable detailed logon auditing
auditpol /set /subcategory:"Credential Validation" /success:enable /failure:enable
auditpol /set /subcategory:"Kerberos Authentication Service" /success:enable /failure:enable
auditpol /set /subcategory:"Kerberos Service Ticket Operations" /success:enable /failure:enable
auditpol /set /subcategory:"Other Logon/Logoff Events" /success:enable /failure:enable

# Enable account management details
auditpol /set /subcategory:"User Account Management" /success:enable /failure:enable
auditpol /set /subcategory:"Computer Account Management" /success:enable /failure:enable
auditpol /set /subcategory:"Security Group Management" /success:enable /failure:enable
auditpol /set /subcategory:"Distribution Group Management" /success:enable /failure:enable
auditpol /set /subcategory:"Application Group Management" /success:enable /failure:enable
auditpol /set /subcategory:"Other Account Management Events" /success:enable /failure:enable

# Enable policy change details
auditpol /set /subcategory:"Audit Policy Change" /success:enable /failure:enable
auditpol /set /subcategory:"Authentication Policy Change" /success:enable /failure:enable
auditpol /set /subcategory:"Authorization Policy Change" /success:enable /failure:enable
auditpol /set /subcategory:"MPSSVC Rule-Level Policy Change" /success:enable /failure:enable
auditpol /set /subcategory:"Filtering Platform Policy Change" /success:enable /failure:enable
auditpol /set /subcategory:"Other Policy Change Events" /success:enable /failure:enable

# Enable privilege use details
auditpol /set /subcategory:"Sensitive Privilege Use" /success:enable /failure:enable
auditpol /set /subcategory:"Non Sensitive Privilege Use" /success:enable /failure:enable
auditpol /set /subcategory:"Other Privilege Use Events" /success:enable /failure:enable
```

### 3. PowerShell Script Block Logging

Enable PowerShell script block logging for enhanced detection:

```powershell
# Enable PowerShell script block logging
Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell\ScriptBlockLogging" -Name "EnableScriptBlockLogging" -Value 1

# Enable PowerShell transcription
Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell\Transcription" -Name "EnableTranscripting" -Value 1
Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell\Transcription" -Name "EnableInvocationHeader" -Value 1
Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell\Transcription" -Name "OutputDirectory" -Value "C:\PowerShell_Logs"
```

### 4. Windows Defender Advanced Threat Protection

Enable Windows Defender ATP logging for enhanced security:

```powershell
# Enable Windows Defender operational logging
wevtutil sl Microsoft-Windows-Windows Defender/Operational /e:true

# Enable Windows Defender client events
wevtutil sl Microsoft-Windows-Windows Defender/Client /e:true
```

## 🔍 Event Log Configuration

### 1. Security Event Log Settings

Configure Security event log for optimal performance:

```powershell
# Set Security log size to 1GB
wevtutil sl Security /ms:1073741824

# Enable Security log
wevtutil sl Security /e:true
```

### 2. System and Application Logs

Configure additional event logs for comprehensive monitoring:

```powershell
# Configure System log
wevtutil sl System /ms:268435456  # 256MB
wevtutil sl System /e:true

# Configure Application log
wevtutil sl Application /ms:268435456  # 256MB
wevtutil sl Application /e:true
```

## 🛡️ Additional Security Hardening

### 1. Enable Sysmon (Optional)

Install and configure Sysmon for enhanced process monitoring:

```powershell
# Download Sysmon
Invoke-WebRequest -Uri "https://download.sysinternals.com/files/Sysmon.zip" -OutFile "Sysmon.zip"
Expand-Archive -Path "Sysmon.zip" -DestinationPath "C:\Sysmon"

# Install Sysmon with default configuration
C:\Sysmon\Sysmon.exe -i

# Or install with custom configuration
C:\Sysmon\Sysmon.exe -i -c C:\Sysmon\sysmon-config.xml
```

### 2. Firewall Logging

Enable Windows Firewall logging for network monitoring:

```powershell
# Enable firewall logging
netsh advfirewall set allprofiles logging filename "C:\Windows\System32\LogFiles\Firewall\pfirewall.log"
netsh advfirewall set allprofiles logging maxfilesize 32767
netsh advfirewall set allprofiles logging droppedconnections enable
netsh advfirewall set allprofiles logging allowedconnections enable
```

### 3. DNS Logging

Enable DNS server logging for network activity monitoring:

```powershell
# Enable DNS debug logging (if DNS server role is installed)
dnscmd /config /debuglevel 0x000000FF
dnscmd /config /logfile "C:\Windows\System32\dns\dns.log"
```

## 📊 Verification Commands

### 1. Check Audit Policy Status

Verify that audit policies are properly configured:

```powershell
# View current audit policy
auditpol /get /category:*

# View specific subcategories
auditpol /get /subcategory:"Credential Validation"
auditpol /get /subcategory:"User Account Management"
auditpol /get /subcategory:"Audit Policy Change"
```

### 2. Check Event Log Status

Verify event log configuration:

```powershell
# Check Security log status
wevtutil gl Security

# Check System log status
wevtutil gl System

# Check Application log status
wevtutil gl Application
```

### 3. Test Logging Configuration

Generate test events to verify logging is working:

```powershell
# Test failed logon (requires wrong credentials)
# This will generate Event ID 4625

# Test successful logon
# This will generate Event ID 4624

# Test account management
# Create a test user to generate Event ID 4720
```

## 🚨 Important Considerations

### 1. Performance Impact

- **Log Size**: Large log files can impact system performance
- **Storage**: Ensure adequate disk space for log files
- **Processing**: High event volume may affect Castellan processing

### 2. Privacy and Compliance

- **Data Retention**: Configure appropriate log retention policies
- **Access Control**: Restrict access to security logs
- **Compliance**: Ensure logging meets regulatory requirements

### 3. Maintenance

- **Log Rotation**: Configure automatic log rotation
- **Monitoring**: Monitor log file sizes and system performance
- **Backup**: Implement log backup strategies

## 🔧 Troubleshooting

### Common Issues

1. **No Security Events Generated**:
   - Verify audit policies are enabled
   - Check Security log is accessible
   - Ensure administrative privileges

2. **High Log Volume**:
   - Adjust audit policy granularity
   - Configure log size limits
   - Implement log filtering

3. **Performance Issues**:
   - Monitor disk I/O
   - Adjust log file sizes
   - Consider log archiving

### Diagnostic Commands

```powershell
# Check for audit policy errors
auditpol /get /category:* | Where-Object { $_.InclusionSetting -eq "No Auditing" }

# Check event log errors
Get-WinEvent -LogName Security -MaxEvents 10 | Format-Table TimeCreated, Id, Message -AutoSize

# Check system performance
Get-Counter "\Memory\Available MBytes"
Get-Counter "\PhysicalDisk(_Total)\% Disk Time"
```

## 📚 Additional Resources

### Microsoft Documentation
- [Advanced Security Audit Policy Settings](https://docs.microsoft.com/en-us/windows/security/threat-protection/auditing/advanced-security-audit-policy-settings)
- [Audit Policy Reference](https://docs.microsoft.com/en-us/windows/security/threat-protection/auditing/audit-policy-reference)
- [PowerShell Script Block Logging](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_logging_windows)

### Security Standards
- [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework)
- [CIS Controls](https://www.cisecurity.org/controls/)
- [ISO 27001](https://www.iso.org/isoiec-27001-information-security.html)

---

**Note**: This configuration provides comprehensive security logging. Adjust settings based on your organization's security requirements and compliance needs.
