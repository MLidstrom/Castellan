# YARA Malware Detection

Castellan's YARA integration provides comprehensive signature-based malware detection capabilities with enterprise-grade rule management.

## 🔥 Features

### Rule Management
- **Full CRUD Operations** - Create, read, update, delete YARA rules via REST API
- **Categorization** - Organize rules by type (Malware, Ransomware, Trojan, Backdoor, Webshell, etc.)
- **MITRE ATT&CK Mapping** - Associate rules with MITRE techniques for threat intelligence
- **Versioning & Rollback** - Rule version history with rollback capabilities
- **Performance Metrics** - Track rule execution time, hit count, and false positives

### Storage & Security
- **Thread-Safe Storage** - Concurrent access with file-based JSON persistence
- **JWT Authentication** - Secure API access with token-based authentication  
- **Validation** - Basic YARA syntax validation and rule testing
- **False Positive Tracking** - Record and manage false positive incidents

## 📡 API Endpoints

### Rule Management
```http
GET    /api/yara-rules                    # List all rules (with pagination)
POST   /api/yara-rules                    # Create new rule
GET    /api/yara-rules/{id}               # Get specific rule
PUT    /api/yara-rules/{id}               # Update rule
DELETE /api/yara-rules/{id}               # Delete rule
```

### Query & Filter
```http
GET    /api/yara-rules?category=Malware           # Filter by category
GET    /api/yara-rules?tag=ransomware             # Filter by tag
GET    /api/yara-rules?mitreTechnique=T1059.001   # Filter by MITRE technique
GET    /api/yara-rules?enabled=true               # Filter enabled rules only
GET    /api/yara-rules?page=2&limit=20            # Pagination support
```

### Utility Endpoints
```http
GET    /api/yara-rules/categories         # List available categories
POST   /api/yara-rules/test               # Test rule against content
POST   /api/yara-rules/{id}/false-positive # Report false positive
GET    /api/yara-rules/{id}/matches       # Get rule match history
```

## 🔧 Configuration

YARA rules are stored in JSON format at:
```
<worker-directory>/data/yara/rules.json
<worker-directory>/data/yara/matches.json
```

### Rule Categories
- **Malware** - General malware detection
- **Ransomware** - Ransomware-specific rules
- **Trojan** - Trojan horse malware
- **Backdoor** - Backdoor and remote access tools
- **Webshell** - Web shell detection
- **Cryptominer** - Cryptocurrency mining malware
- **Exploit** - Exploit detection rules
- **Suspicious** - Suspicious behavior patterns
- **PUA** - Potentially Unwanted Applications
- **Custom** - User-defined categories

## 💡 Usage Examples

### Create a YARA Rule
```json
POST /api/yara-rules
{
  "name": "Suspicious_PowerShell_Commands",
  "description": "Detects suspicious PowerShell command patterns",
  "ruleContent": "rule Suspicious_PowerShell {\n  strings:\n    $a = \"Invoke-Expression\" nocase\n    $b = \"DownloadString\" nocase\n  condition:\n    any of them\n}",
  "category": "Malware",
  "author": "Security Team",
  "isEnabled": true,
  "priority": 75,
  "threatLevel": "High",
  "mitreTechniques": ["T1059.001"],
  "tags": ["powershell", "suspicious", "command-execution"]
}
```

### Filter Rules by Category
```bash
GET /api/yara-rules?category=Ransomware&enabled=true
```

### Test Rule Against Content
```json
POST /api/yara-rules/test
{
  "ruleContent": "rule Test { strings: $a = \"malware\" condition: $a }",
  "testContent": "This content contains malware signatures"
}
```

## 🔍 Rule Metrics

Each rule tracks comprehensive metrics:
- **Hit Count** - Number of successful matches
- **False Positive Count** - Reported false positives
- **Average Execution Time** - Performance metrics
- **Last Match** - Timestamp of most recent match
- **Version History** - Rule modification tracking

## 🛡️ Security Features

### Authentication
All YARA API endpoints require JWT authentication:
```http
Authorization: Bearer <jwt-token>
```

### Validation
- **Syntax Validation** - Basic YARA rule syntax checking
- **Content Validation** - Rule content and metadata validation
- **Permission Checks** - Role-based access control

### Audit Trail
- Rule creation, modification, and deletion logging
- Match result tracking with timestamps
- False positive incident recording
- Performance metric collection

## 🚀 Integration

### Dependencies
```xml
<PackageReference Include="dnYara" Version="2.1.0" />
<PackageReference Include="dnYara.NativePack" Version="2.1.0.3" />
```

### Service Registration
```csharp
// Program.cs
builder.Services.AddSingleton<IYaraRuleStore, FileBasedYaraRuleStore>();
```

### Future Enhancements
- **Real-time Scanning** - Integration with file system monitoring
- **Memory Scanning** - Process memory analysis with YARA rules
- **Pipeline Integration** - Automatic rule execution on security events
- **Community Rules** - Import/export capabilities for rule sharing
- **Rule Compiler** - Pre-compiled rules for better performance

## 📚 Resources

- [YARA Documentation](https://yara.readthedocs.io/)
- [dnYara .NET Library](https://github.com/Microsoft/yara-dotnet)
- [MITRE ATT&CK Framework](https://attack.mitre.org/)
- [Castellan API Documentation](API.md)
