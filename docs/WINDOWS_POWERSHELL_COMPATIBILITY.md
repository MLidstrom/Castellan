# Windows PowerShell 5.1 Compatibility Guide

## Overview

All Castellan PowerShell scripts are designed to work seamlessly with native **Windows PowerShell 5.1** (the version that ships with Windows). This document outlines the compatibility features and optimizations implemented to ensure reliable operation on Windows systems.

## Compatibility Features

### ✅ **Full Windows PowerShell 5.1 Support**

Our scripts include specific optimizations for Windows PowerShell 5.1:

- **TLS 1.2 Configuration**: Automatically configured for secure web requests
- **UseBasicParsing Parameter**: Avoids Internet Explorer dependency issues
- **Timeout Handling**: 5-second timeouts prevent hanging operations
- **Error Handling**: Robust error handling for network and system operations
- **CIM/WMI Support**: Uses Get-CimInstance for reliable system information

### 🔧 **Technical Optimizations**

#### **Web Requests**
```powershell
# Windows PowerShell 5.1 compatible web requests
$response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5 -ErrorAction SilentlyContinue
```

#### **TLS Security**
```powershell
# Ensure TLS 1.2 for secure connections on older PowerShell versions
if ($PSVersionTable.PSVersion.Major -lt 6) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}
```

#### **Process Detection**
```powershell
# Use CIM for reliable command line detection
$processes = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object {
    $_.CommandLine -like "*Castellan.Worker*"
}
```

## Script Features

### 📁 **Enhanced Scripts**

| Script | Windows PowerShell Features |
|--------|------------------------------|
| `start.ps1` | TLS 1.2, UseBasicParsing, robust Docker/Ollama detection |
| `status.ps1` | CIM-based process detection, timeout handling, structured output |
| `stop.ps1` | Multi-method process detection, graceful shutdown attempts |
| `run-tests.ps1` | Native .NET test runner integration |
| `test-ps51-compatibility.ps1` | Comprehensive compatibility testing |

### 🚀 **Usage Examples**

#### **Basic Operations**
```powershell
# Check system compatibility
.\scripts\test-ps51-compatibility.ps1

# Start Castellan (all services)
.\scripts\start.ps1

# Check service status
.\scripts\status.ps1

# Stop all services
.\scripts\stop.ps1
```

#### **Advanced Usage**
```powershell
# Start in background without building
.\scripts\start.ps1 -Background -NoBuild

# Detailed status with verbose output
.\scripts\status.ps1 -Detailed

# Force stop with Qdrant shutdown
.\scripts\stop.ps1 -Force -StopQdrant

# Run tests with verbose output
.\scripts\run-tests.ps1 -Verbosity detailed
```

## Compatibility Testing

### 🧪 **Built-in Test Suite**

Run the compatibility test to verify your system:

```powershell
.\scripts\test-ps51-compatibility.ps1
```

**Test Coverage:**
- ✅ PowerShell version compatibility
- ✅ TLS 1.2 configuration  
- ✅ Web request functionality
- ✅ Process management
- ✅ CIM/WMI support
- ✅ Script syntax validation

### 📊 **Expected Results**

On a compatible Windows system, you should see:
```
SUCCESS: All tests passed (6/6)
Your system is fully compatible with Castellan PowerShell scripts!
```

## Version Support Matrix

| PowerShell Version | Status | Notes |
|-------------------|---------|-------|
| **Windows PowerShell 5.1** | ✅ **Full Support** | Primary target, all features tested |
| **PowerShell 7.x** | ✅ **Full Support** | Cross-platform, enhanced features |
| **Windows PowerShell 5.0** | ⚠️ **Limited Support** | May work but not tested |
| **Windows PowerShell 4.0** | ❌ **Not Supported** | Missing required features |

## Troubleshooting

### 🔍 **Common Issues**

#### **Internet Explorer Dependencies**
If you see errors about Internet Explorer:
```powershell
# Ensure -UseBasicParsing is used (already implemented in our scripts)
Invoke-WebRequest -Uri $url -UseBasicParsing
```

#### **TLS/SSL Errors**
If web requests fail with SSL errors:
```powershell
# TLS 1.2 should be automatically configured, but you can verify:
[Net.ServicePointManager]::SecurityProtocol
```

#### **Execution Policy Issues**
If scripts won't run:
```powershell
# Check current policy
Get-ExecutionPolicy

# Set for current user (recommended)
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser

# Or for specific session
Set-ExecutionPolicy Bypass -Scope Process
```

### 🛠️ **Validation Steps**

1. **Check PowerShell Version**:
   ```powershell
   $PSVersionTable
   ```

2. **Test Basic Functionality**:
   ```powershell
   .\scripts\test-ps51-compatibility.ps1
   ```

3. **Validate Scripts**:
   ```powershell
   .\scripts\validate_ps.ps1
   ```

4. **Test Web Connectivity**:
   ```powershell
   Invoke-WebRequest -Uri "http://httpbin.org/get" -UseBasicParsing
   ```

## Performance Considerations

### ⚡ **Optimizations**

- **Timeout Management**: 5-second timeouts prevent hanging
- **Error Suppression**: Silent failures where appropriate
- **Process Caching**: Efficient process detection methods
- **Resource Cleanup**: Proper cleanup of temporary resources

### 📈 **Expected Performance**

| Operation | Expected Time | Notes |
|-----------|---------------|-------|
| Status Check | < 5 seconds | Network-dependent |
| Service Start | 10-30 seconds | Build-dependent |
| Service Stop | < 10 seconds | Graceful shutdown |
| Compatibility Test | < 10 seconds | System-dependent |

## Best Practices

### 📋 **Recommendations**

1. **Always test compatibility first**:
   ```powershell
   .\scripts\test-ps51-compatibility.ps1
   ```

2. **Use parameter validation**:
   ```powershell
   # Scripts include built-in parameter validation
   .\scripts\start.ps1 -?
   ```

3. **Check logs for issues**:
   ```powershell
   .\scripts\status.ps1 -Detailed
   ```

4. **Keep Windows PowerShell updated**:
   - Windows PowerShell 5.1 is the latest version
   - Consider PowerShell 7.x for additional features

## Support Information

### 🔧 **System Requirements**

- **OS**: Windows 10/11 (any edition)
- **PowerShell**: 5.1 or later
- **Execution Policy**: RemoteSigned or Bypass
- **Network**: Internet connectivity for downloads
- **Permissions**: User-level permissions sufficient

### 📞 **Getting Help**

If you encounter compatibility issues:

1. Run the compatibility test first
2. Check the troubleshooting section above
3. Review logs in the `logs/` directory
4. Open an issue with test results and system information

---

**Document Version**: 1.1  
**Last Updated**: January 2025  
**Tested On**: Windows PowerShell 5.1.26100.4768  
**Compilation Status**: ✅ **Fixed and Verified**  
**Status**: ✅ **Fully Compatible**
