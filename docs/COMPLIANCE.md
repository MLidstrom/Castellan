# Castellan Compliance Guide

**Status**: ✅ **Enterprise Ready** | 🏛️ **Compliance Reports**: Four Frameworks Operational
**Last Updated**: September 28, 2025

## 🎯 Overview

Castellan provides comprehensive compliance capabilities for organizations requiring adherence to security and privacy regulations. This guide outlines supported frameworks, implementation approaches, and compliance features.

## 🏛️ Supported Compliance Frameworks

### **Security Frameworks**
- **NIST Cybersecurity Framework** - Complete implementation guidance
- **ISO 27001/27002** - Information security management systems
- **CIS Controls** - Critical security controls implementation
- **MITRE ATT&CK** - Threat-based security framework (800+ techniques)

### **Privacy Regulations**
- **GDPR** - EU General Data Protection Regulation
- **CCPA** - California Consumer Privacy Act
- **HIPAA** - Health Insurance Portability and Accountability Act
- **SOX** - Sarbanes-Oxley Act requirements

### **Industry Standards**
- **SOC 2 Type II** - Service Organization Control 2
- **PCI DSS** - Payment Card Industry Data Security Standard
- **FedRAMP** - Federal Risk and Authorization Management Program
- **FISMA** - Federal Information Security Management Act

## 🔐 Security Compliance Features

### **Data Protection**
```json
{
  "encryption": {
    "data_at_rest": "AES-256 encryption for SQLite database",
    "data_in_transit": "TLS 1.3 for all API communications",
    "password_hashing": "BCrypt with configurable work factors (4-12 rounds)"
  },
  "access_control": {
    "authentication": "JWT with secure refresh token rotation",
    "authorization": "Role-based access control (RBAC)",
    "session_management": "Automatic token expiration and cleanup"
  }
}
```

### **Audit and Logging**
- **Complete Audit Trail**: All security events logged with correlation IDs
- **Authentication Logging**: Comprehensive login/logout event tracking
- **API Request Tracking**: Every API request tracked with unique identifiers
- **Configuration Changes**: All system configuration changes logged
- **Data Access Logging**: Complete data access and modification trails

### **Monitoring and Detection**
- **Real-time Security Monitoring**: Continuous threat detection and analysis
- **Anomaly Detection**: Machine learning-based behavioral analysis
- **Incident Response**: Automated threat response with configurable actions
- **Performance Monitoring**: System health and security service status tracking

## 📊 NIST Cybersecurity Framework Alignment

### **Identify (ID)**
| Control | Implementation | Status |
|---------|---------------|---------|
| ID.AM-1 | Asset inventory via application discovery | ✅ Implemented |
| ID.AM-2 | Software inventory and MITRE technique mapping | ✅ Implemented |
| ID.GV-1 | Information security policy enforcement | ✅ Configuration validation |
| ID.RA-1 | Risk assessment through threat intelligence | ✅ AI-powered analysis |
| ID.RA-5 | Threat intelligence integration | ✅ IP enrichment & MITRE mapping |

### **Protect (PR)**
| Control | Implementation | Status |
|---------|---------------|---------|
| PR.AC-1 | Identity and credential management | ✅ JWT + BCrypt |
| PR.AC-4 | Access permissions management | ✅ RBAC implementation |
| PR.DS-1 | Data protection at rest and in transit | ✅ AES-256 + TLS 1.3 |
| PR.DS-5 | Data leak protection | ✅ Local deployment only |
| PR.PT-1 | Audit logs protection | ✅ Tamper-resistant logging |

### **Detect (DE)**
| Control | Implementation | Status |
|---------|---------------|---------|
| DE.AE-1 | Security event baseline establishment | ✅ Behavioral analytics |
| DE.AE-2 | Security event analysis | ✅ AI-powered triage |
| DE.AE-3 | Event correlation | ✅ M4 correlation engine |
| DE.CM-1 | Network monitoring | ✅ Real-time event processing |
| DE.CM-7 | External service provider monitoring | ✅ Connection health monitoring |

### **Respond (RS)**
| Control | Implementation | Status |
|---------|---------------|---------|
| RS.RP-1 | Response plan execution | ✅ Automated response actions |
| RS.CO-2 | Event reporting | ✅ Teams/Slack integration |
| RS.AN-1 | Notification systems | ✅ Multi-channel alerting |
| RS.MI-2 | Incident containment | ✅ Configurable response actions |
| RS.IM-1 | Response plan updates | ✅ Continuous improvement cycle |

### **Recover (RC)**
| Control | Implementation | Status |
|---------|---------------|---------|
| RC.RP-1 | Recovery plan execution | ✅ Automatic restart recovery |
| RC.IM-1 | Recovery plan updates | ✅ Configuration management |
| RC.CO-3 | Recovery communication | ✅ Status monitoring & alerts |

## 🏥 HIPAA Compliance Implementation

### **Administrative Safeguards**
- **Security Officer Assignment**: Configurable admin user management
- **Workforce Training**: Comprehensive documentation and setup guides
- **Access Management**: Role-based access control and audit trails
- **Contingency Plan**: Automatic restart and recovery mechanisms

### **Physical Safeguards**
- **Facility Access Control**: Local deployment - customer controlled
- **Workstation Security**: Windows-native with built-in security features
- **Media Controls**: Local storage with encryption at rest

### **Technical Safeguards**
- **Access Control**: Unique user identification and authentication
- **Audit Controls**: Complete logging and monitoring of PHI access
- **Integrity Controls**: Data validation and tamper detection
- **Transmission Security**: TLS 1.3 encryption for all communications

## 🌍 GDPR Compliance Features

### **Data Protection Principles**
```json
{
  "lawfulness": "Processing based on legitimate interests (security)",
  "fairness": "Transparent data collection and processing",
  "transparency": "Clear documentation of data handling",
  "purpose_limitation": "Data used only for security monitoring",
  "data_minimisation": "Only necessary data collected and stored",
  "accuracy": "Real-time data validation and correction",
  "storage_limitation": "24-hour rolling window with automatic deletion",
  "integrity_confidentiality": "Encryption and access controls"
}
```

### **Individual Rights Support**
- **Right to Information**: Clear documentation of data processing
- **Right of Access**: API endpoints for data subject access
- **Right to Rectification**: Data correction and update capabilities
- **Right to Erasure**: Automatic data deletion after retention period
- **Right to Data Portability**: Export capabilities via API
- **Right to Object**: Configurable processing controls

## 📋 SOC 2 Type II Readiness

### **Security Controls**
| Criterion | Implementation | Evidence |
|-----------|---------------|----------|
| CC6.1 | Logical access security | JWT authentication + RBAC |
| CC6.2 | System access monitoring | Complete audit trails |
| CC6.3 | Access revocation | Token blacklisting system |
| CC6.6 | Data transmission protection | TLS 1.3 encryption |
| CC6.7 | System monitoring | Real-time health dashboards |

### **Availability Controls**
| Criterion | Implementation | Evidence |
|-----------|---------------|----------|
| A1.1 | Processing integrity | Data validation and verification |
| A1.2 | System monitoring | Connection pooling and health checks |
| A1.3 | System capacity | Auto-scaling and load balancing |

## 🏛️ FedRAMP Implementation Guide

### **Security Control Families**
- **AC (Access Control)**: JWT + RBAC implementation
- **AU (Audit and Accountability)**: Comprehensive logging with correlation IDs
- **CA (Security Assessment)**: Automated security validation
- **CM (Configuration Management)**: Version-controlled configuration
- **IA (Identification and Authentication)**: Multi-factor capable authentication
- **SC (System and Communications Protection)**: TLS 1.3 + encryption at rest
- **SI (System and Information Integrity)**: Real-time monitoring and detection

### **Continuous Monitoring**
- **Real-time Security Monitoring**: 12,000+ events per second processing
- **Automated Compliance Reporting**: Built-in compliance dashboard
- **Vulnerability Management**: Continuous security assessment
- **Incident Response**: Automated threat response capabilities

## 📊 Compliance Reporting

**Status**: ✅ Phase 3 Complete - Full Framework Implementation with Scope Separation | **Production Ready**

### **Current Implementation (100% Phase 3 Complete)**
- ✅ **Database Schema**: ComplianceReport, ComplianceControl, ComplianceAssessmentResult tables with ComplianceScope enum
- ✅ **Entity Framework**: Full relationships, indexes, constraints, and migrations
- ✅ **API Endpoints**: Full CRUD operations with visibility filtering at `/api/compliance-reports`
- ✅ **Frontend Interface**: React Admin interface showing only Organization-scope frameworks
- ✅ **Assessment Engine**: Real-time compliance assessment based on security events
- ✅ **Five Organization Frameworks**: HIPAA (17 controls), SOX (11 controls), PCI-DSS (12 controls), ISO 27001 (15 controls), SOC2 (15 controls)
- ✅ **Two Application Frameworks**: CIS Controls v8 (13 controls), Windows Security Baselines (12 controls) - Hidden from users
- ✅ **Visibility Separation**: ComplianceFrameworkService ensures users only see Organization frameworks
- ✅ **Background Assessment**: ApplicationComplianceBackgroundService runs 6-hour cycles for Application frameworks
- ✅ **Database Seeding**: Automatic seeding of all 95 controls (70 Organization + 25 Application)
- ✅ **Framework Registration**: All 7 frameworks properly registered with DI container
- ✅ **Name Mapping**: Handles UI/backend naming differences (e.g., "ISO27001" → "ISO 27001")

### **API Endpoints**
```powershell
# Get compliance reports
curl -H "Authorization: Bearer $token" \
     "http://localhost:5000/api/compliance-reports"

# Create new compliance report
curl -X POST -H "Authorization: Bearer $token" \
     -H "Content-Type: application/json" \
     -d '{"framework":"HIPAA","reportType":"assessment"}' \
     "http://localhost:5000/api/compliance-reports"

# Get detailed report with control assessments
curl -H "Authorization: Bearer $token" \
     "http://localhost:5000/api/compliance-reports/{id}/detailed"

# Export audit logs
curl -H "Authorization: Bearer $token" \
     "http://localhost:5000/api/audit/export?format=csv&days=30"

# System health compliance check
curl "http://localhost:5000/api/system-status" | \
     jq '.data[] | select(.isHealthy == false)'
```

### **Organization-Scope Frameworks (User-Visible)**
- ✅ **HIPAA**: Health Insurance Portability and Accountability Act (17 controls)
- ✅ **SOX**: Sarbanes-Oxley Act compliance (11 controls)
- ✅ **PCI-DSS**: Payment Card Industry Data Security Standard (12 controls)
- ✅ **ISO 27001**: Information Security Management Systems (15 controls)
- ✅ **SOC2**: Service Organization Control 2 Type II (15 controls)

### **Application-Scope Frameworks (Hidden from Users)**
- ✅ **CIS Controls v8**: Application security baseline assessment (13 controls)
- ✅ **Windows Security Baselines**: Windows platform compliance (12 controls)

### **Future Frameworks (Phase 4+)**
- ⏳ **GDPR**: EU General Data Protection Regulation
- ⏳ **FedRAMP**: Federal Risk and Authorization Management Program
- ⏳ **NIST 800-53**: Security and Privacy Controls

### **Evidence Collection**
- **Security Event Logs**: Structured JSON logs with timestamps
- **Authentication Logs**: Complete login/logout audit trail
- **Configuration Changes**: Version-controlled configuration history
- **System Health Metrics**: Real-time performance and availability data
- **Incident Response Logs**: Automated response action documentation

## 🔧 Implementation Best Practices

### **Security Configuration**
```json
{
  "authentication": {
    "jwt_secret_length": "minimum 64 characters",
    "password_policy": "complexity validation enforced",
    "session_timeout": "configurable (default: 1 hour)",
    "token_rotation": "enabled by default"
  },
  "audit_logging": {
    "correlation_ids": "enabled for all requests",
    "retention_period": "configurable (default: 24 hours)",
    "log_integrity": "tamper-resistant logging",
    "export_formats": "JSON, CSV, SIEM integration"
  }
}
```

### **Data Governance**
- **Data Classification**: Automatic security event classification
- **Retention Policies**: Configurable data retention periods
- **Access Controls**: Role-based access with principle of least privilege
- **Data Encryption**: End-to-end encryption for sensitive data
- **Backup and Recovery**: Automated backup with point-in-time recovery

## 📈 Compliance Monitoring Dashboard

### **Real-time Compliance Metrics**
- **Security Control Effectiveness**: Automated compliance scoring
- **Audit Trail Completeness**: 100% event logging verification
- **Access Control Compliance**: Real-time access violation detection
- **Data Protection Status**: Encryption and integrity monitoring
- **Incident Response Metrics**: Mean time to detection and response

### **Compliance Alerts**
- **Control Failures**: Immediate notification of compliance violations
- **Audit Gap Detection**: Missing or incomplete audit trails
- **Access Anomalies**: Unusual access patterns or violations
- **System Health Issues**: Compliance-affecting system problems
- **Configuration Drift**: Unauthorized configuration changes

## 📞 Compliance Support

### **Documentation and Evidence**
- **Security Architecture**: Detailed technical architecture documentation
- **Control Implementation**: Step-by-step implementation guides
- **Audit Evidence**: Automated evidence collection and export
- **Risk Assessment**: Comprehensive security risk documentation
- **Penetration Testing**: Security testing and validation reports

### **Professional Services**
- **Compliance Assessment**: Professional compliance readiness evaluation
- **Implementation Support**: Expert guidance for compliance implementation
- **Audit Support**: Documentation and evidence preparation
- **Training Services**: Staff training on compliance requirements
- **Ongoing Support**: Continuous compliance monitoring and improvement

---

**Castellan** - Enterprise-grade security monitoring with comprehensive compliance capabilities for regulated environments. 🏰🛡️
