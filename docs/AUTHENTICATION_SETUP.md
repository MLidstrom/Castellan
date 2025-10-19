# Authentication Setup Guide - Castellan

## Overview

The hardcoded credentials have been removed from Castellan and replaced with a secure configuration system. Authentication credentials and JWT secrets must now be configured via environment variables or configuration files.

## Critical Security Changes

### What Was Fixed
- **Hardcoded credentials removed** from AuthController.cs
- **JWT secret externalized** from source code  
- **Configuration-based authentication** implemented
- **Environment variable support** added

## Configuration Methods

### Option 1: Environment Variables (Recommended for Production)

Set these environment variables:

```powershell
# JWT Configuration
$env:AUTHENTICATION__JWT__SECRETKEY = "your-very-secure-jwt-secret-key-at-least-64-characters-long"

# Admin User Credentials  
$env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
$env:AUTHENTICATION__ADMINUSER__PASSWORD = "your-secure-password-here"
```

### Option 2: Configuration File (Development Only)

Update `appsettings.json`:

```json
{
  "Authentication": {
    "Jwt": {
      "SecretKey": "your-very-secure-jwt-secret-key-at-least-64-characters-long-please",
      "Issuer": "castellan-security",
      "Audience": "dashboard",
      "ExpirationHours": 24
    },
    "AdminUser": {
      "Username": "admin", 
      "Password": "your-secure-password-here",
      "Email": "admin@castellan.security",
      "FirstName": "Castellan",
      "LastName": "Administrator"
    }
  }
}
```

## Quick Setup for Development

1. **Copy the example configuration:**
   ```powershell
   copy src\Castellan.Worker\appsettings.template.json src\Castellan.Worker\appsettings.json
   ```

2. **Edit the development configuration** and set secure values:
   - Choose a strong JWT secret key (64+ characters)
   - Set a secure admin password (8+ characters, mixed case, numbers, symbols)

3. **Start the service:**
   ```powershell
   cd src\Castellan.Worker
   dotnet run
   ```

## Security Requirements

### JWT Secret Key
- **Minimum length:** 64 characters
- **Complexity:** Mix of letters, numbers, and symbols
- **Uniqueness:** Different for each environment
- **Storage:** Environment variables only (never in source control)

### Admin Password  
- **Minimum length:** 8 characters
- **Complexity:** Upper/lower case, numbers, symbols
- **Storage:** Environment variables or secure config (never hardcoded)

## Production Deployment

### Environment Variables
```powershell
$env:AUTHENTICATION__JWT__SECRETKEY = "your-production-jwt-secret-64-chars-minimum"
$env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
$env:AUTHENTICATION__ADMINUSER__PASSWORD = "your-production-secure-password"
```

### Docker Configuration
```dockerfile
ENV AUTHENTICATION__JWT__SECRETKEY="your-production-jwt-secret-64-chars-minimum"
ENV AUTHENTICATION__ADMINUSER__USERNAME="admin"
ENV AUTHENTICATION__ADMINUSER__PASSWORD="your-production-secure-password"
```

## Error Handling

### Common Errors

#### "JWT SecretKey not configured"
- **Cause:** Missing or empty `AUTHENTICATION__JWT__SECRETKEY`
- **Fix:** Set the environment variable or appsettings.json value

#### "Authentication not configured"
- **Cause:** Missing or empty username/password configuration
- **Fix:** Set `AUTHENTICATION__ADMINUSER__USERNAME` and `AUTHENTICATION__ADMINUSER__PASSWORD`

#### "Invalid credentials"
- **Cause:** Wrong username or password provided
- **Fix:** Verify configured credentials match login attempt

### Debugging Authentication Issues

1. **Check configuration loading:**
   ```powershell
   # Logs will show if configuration is missing
   dotnet run --environment Development
   ```

2. **Verify environment variables:**
   ```powershell
   echo $env:AUTHENTICATION__JWT__SECRETKEY
   echo $env:AUTHENTICATION__ADMINUSER__USERNAME
   ```

3. **Test with curl:**
```powershell
Invoke-RestMethod -Method POST -Uri 'http://localhost:5000/api/auth/login' -ContentType 'application/json' -Body '{"username":"your-username","password":"your-password"}'
```

## Security Best Practices

### Development
- Use `appsettings.Development.json` (not tracked in git)
- Set different credentials for each developer
- Never commit credentials to version control

### Production  
- Use environment variables exclusively
- Rotate JWT secrets regularly (recommended: monthly)
- Use strong, unique passwords
- Monitor authentication logs for suspicious activity
- Consider implementing password hashing (planned future enhancement)

---

**IMPORTANT:** Never commit authentication credentials to source control. Use environment variables or secure configuration management systems in production.