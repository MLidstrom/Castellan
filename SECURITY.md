# Security Policy

Castellan is an AI-powered Windows security monitoring platform. We take security seriously and welcome responsible disclosure of vulnerabilities.

## Reporting a Vulnerability

**Please do not report security issues via public GitHub issues.**

Use one of the following private channels:
- **GitHub Security Advisory**: https://github.com/MLidstrom/Castellan/security/advisories/new (preferred)
- **Direct Contact**: Contact maintainers through GitHub Issues with `[SECURITY]` prefix for coordination

When reporting, include:
- A concise description of the issue and potential impact
- Steps to reproduce (proof-of-concept if possible)
- Affected version/commit (tag/branch/commit SHA)
- Any configuration details needed to reproduce

## Disclosure Policy
- We will acknowledge receipt within 72 hours.
- We will coordinate a fix and advisory; we may request more details.
- We will credit reporters upon request (or keep you anonymous).
- Please avoid public disclosure for 90 days or until a fix is released, whichever comes first.

## Current Security Features

Castellan implements several security measures:

### Authentication & Authorization
- **BCrypt Password Hashing**: Secure password storage with salt generation
- **JWT Token Authentication**: Secure API access with configurable expiration
- **JWT Token Blacklisting**: Server-side token invalidation for logout
- **Refresh Token System**: Proper token rotation and revocation with audit trail
- **Environment-based Configuration**: No hardcoded credentials in source code

### Data Security
- **Input Validation**: Comprehensive validation on all API endpoints
- **SQL Injection Protection**: Parameterized queries throughout
- **CORS Configuration**: Controlled cross-origin access
- **Structured Logging**: Security event tracking with correlation IDs

## Safe Harbor
We support good-faith research:
- Do not exploit beyond what is necessary to demonstrate impact
- Avoid privacy violations, data exfiltration, service degradation, or persistence
- Only test against systems you own or have explicit permission to test
- Use the private channels above for all vulnerability details

## Security Best Practices

### For Administrators
- **Authentication Setup**: See [AUTHENTICATION_SETUP.md](docs/AUTHENTICATION_SETUP.md) for secure configuration
- **Environment Variables**: Use environment variables for all secrets (JWT keys, passwords)
- **Strong Passwords**: Minimum 8 characters with mixed case, numbers, and symbols
- **JWT Secret Keys**: Minimum 64 characters with high entropy
- **Regular Updates**: Keep dependencies and Docker images updated

### For Developers
- Never commit secrets or credentials to version control
- Use parameterized queries for all database operations
- Validate all user inputs at API boundaries
- Follow principle of least privilege for service accounts
- Implement proper error handling without information leakage

## Security Considerations Specific to Castellan

### Windows Event Log Processing
- Castellan processes Windows Event Logs which may contain sensitive information
- Event data is processed locally and not transmitted externally by default
- Consider data retention policies for processed security events

### AI/LLM Integration
- Local LLM processing (Ollama) keeps data on-premises
- OpenAI integration (if enabled) may transmit data externally - review your privacy requirements
- Vector embeddings are stored locally in Qdrant database

### Network Security
- Default configuration binds to localhost (127.0.0.1) only
- Admin interface (port 8080) and API (port 5000) should be secured if exposed
- Consider using HTTPS/TLS in production environments

## Contact

For questions about this security policy: open a [GitHub Discussion](https://github.com/MLidstrom/Castellan/discussions) or use the private reporting channels above for sensitive matters.
