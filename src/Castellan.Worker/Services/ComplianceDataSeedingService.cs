using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using Castellan.Worker.Models.Compliance;

namespace Castellan.Worker.Services;

public class ComplianceDataSeedingService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ComplianceDataSeedingService> _logger;

    public ComplianceDataSeedingService(
        IServiceProvider serviceProvider,
        ILogger<ComplianceDataSeedingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CastellanDbContext>();

        try
        {
            _logger.LogInformation("Starting compliance data seeding...");

            await SeedHipaaControlsAsync(context);
            await SeedSoxControlsAsync(context);
            await SeedPciDssControlsAsync(context);
            await SeedIso27001ControlsAsync(context);
            await SeedSOC2ControlsAsync(context);

            // Application-scope frameworks (hidden from users)
            await SeedCISControlsAsync(context);
            await SeedWindowsSecurityBaselinesAsync(context);

            _logger.LogInformation("Compliance data seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compliance data seeding");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedHipaaControlsAsync(CastellanDbContext context)
    {
        // Check if HIPAA controls already exist
        var existingCount = await context.ComplianceControls
            .Where(c => c.Framework == "HIPAA")
            .CountAsync();

        if (existingCount > 0)
        {
            _logger.LogInformation("HIPAA controls already seeded ({Count} controls)", existingCount);
            return;
        }

        _logger.LogInformation("Seeding HIPAA compliance controls...");

        var hipaaControls = new List<ComplianceControl>
        {
            // Administrative Safeguards (164.308)
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.308(a)(1)(i)",
                ControlName = "Security Officer",
                Description = "Assign a security officer responsible for developing and implementing security policies and procedures.",
                Category = "Administrative Safeguards",
                Priority = "High",
                IsUserVisible = true, // Visible to users - Organization scope
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "Healthcare",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.308(a)(3)(i)",
                ControlName = "Workforce Training",
                Description = "Implement procedures for authorizing access to electronic protected health information.",
                Category = "Administrative Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.308(a)(4)(i)",
                ControlName = "Access Management",
                Description = "Implement procedures for granting access to electronic protected health information.",
                Category = "Administrative Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.308(a)(5)(i)",
                ControlName = "Security Awareness",
                Description = "Implement security awareness and training programs for all workforce members.",
                Category = "Administrative Safeguards",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },

            // Physical Safeguards (164.310)
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.310(a)(1)",
                ControlName = "Facility Access Controls",
                Description = "Implement procedures to limit physical access to electronic systems and equipment.",
                Category = "Physical Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.310(b)",
                ControlName = "Workstation Use",
                Description = "Implement procedures for workstation access and use.",
                Category = "Physical Safeguards",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.310(c)",
                ControlName = "Device and Media Controls",
                Description = "Implement procedures for receipt and removal of hardware and electronic media.",
                Category = "Physical Safeguards",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },

            // Technical Safeguards (164.312)
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.312(a)(1)",
                ControlName = "Unique User Identification",
                Description = "Assign a unique name and/or number for identifying and tracking user identity.",
                Category = "Technical Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.312(a)(2)(i)",
                ControlName = "Automatic Logoff",
                Description = "Implement procedures for terminating an electronic session after a predetermined time of inactivity.",
                Category = "Technical Safeguards",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.312(b)",
                ControlName = "Audit Controls",
                Description = "Implement hardware, software, and/or procedural mechanisms for recording access to electronic protected health information.",
                Category = "Technical Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.312(c)(1)",
                ControlName = "Integrity Controls",
                Description = "Implement procedures to ensure electronic protected health information is not improperly altered or destroyed.",
                Category = "Technical Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.312(d)",
                ControlName = "Person Authentication",
                Description = "Implement procedures to verify that a person seeking access is the one claimed.",
                Category = "Technical Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.312(e)(1)",
                ControlName = "Transmission Security",
                Description = "Implement procedures to guard against unauthorized access to electronic protected health information being transmitted.",
                Category = "Technical Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },

            // Additional key HIPAA controls
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.308(a)(6)(i)",
                ControlName = "Security Incident Procedures",
                Description = "Implement procedures to address security incidents.",
                Category = "Administrative Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.308(a)(7)(i)",
                ControlName = "Contingency Plan",
                Description = "Establish procedures for responding to an emergency or other occurrence.",
                Category = "Administrative Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.308(a)(8)",
                ControlName = "Evaluation",
                Description = "Perform a periodic technical and nontechnical evaluation of the effectiveness of security controls.",
                Category = "Administrative Safeguards",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "HIPAA",
                ControlId = "164.312(a)(2)(ii)",
                ControlName = "Encryption and Decryption",
                Description = "Implement mechanisms to encrypt and decrypt electronic protected health information.",
                Category = "Technical Safeguards",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.ComplianceControls.AddRangeAsync(hipaaControls);
        await context.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded {Count} HIPAA compliance controls", hipaaControls.Count);
    }

    private async Task SeedSoxControlsAsync(CastellanDbContext context)
    {
        // Check if SOX controls already exist
        var existingCount = await context.ComplianceControls
            .Where(c => c.Framework == "SOX")
            .CountAsync();

        if (existingCount > 0)
        {
            _logger.LogInformation("SOX controls already seeded ({Count} controls)", existingCount);
            return;
        }

        _logger.LogInformation("Seeding SOX compliance controls...");

        var soxControls = new List<ComplianceControl>
        {
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-302",
                ControlName = "Financial Reporting Controls",
                Description = "Procedures for accurate financial reporting and disclosure controls",
                Category = "Financial Reporting",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-404",
                ControlName = "Internal Controls Assessment",
                Description = "Assessment of internal control effectiveness over financial reporting",
                Category = "Internal Controls",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-ITGC-01",
                ControlName = "IT Access Controls",
                Description = "Controls for managing access to financial systems and data",
                Category = "IT General Controls",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-ITGC-02",
                ControlName = "Change Management",
                Description = "Controls for managing changes to financial systems",
                Category = "IT General Controls",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-ITGC-03",
                ControlName = "Data Backup and Recovery",
                Description = "Controls for backup and recovery of financial data",
                Category = "IT General Controls",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-ITGC-04",
                ControlName = "System Operations",
                Description = "Controls for IT operations affecting financial systems",
                Category = "IT General Controls",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-ITGC-05",
                ControlName = "Segregation of Duties",
                Description = "Controls for appropriate segregation of duties in financial processes",
                Category = "IT General Controls",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-906",
                ControlName = "Code of Ethics",
                Description = "Code of ethics for senior financial officers",
                Category = "Ethics and Compliance",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-301",
                ControlName = "Audit Committee",
                Description = "Independent audit committee requirements",
                Category = "Corporate Governance",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-409",
                ControlName = "Real-time Disclosure",
                Description = "Real-time disclosure of material changes in financial condition",
                Category = "Financial Reporting",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOX",
                ControlId = "SOX-401",
                ControlName = "Enhanced Financial Disclosures",
                Description = "Enhanced disclosures in periodic reports",
                Category = "Financial Reporting",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.ComplianceControls.AddRangeAsync(soxControls);
        await context.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded {Count} SOX compliance controls", soxControls.Count);
    }

    private async Task SeedPciDssControlsAsync(CastellanDbContext context)
    {
        // Check if PCI DSS controls already exist
        var existingCount = await context.ComplianceControls
            .Where(c => c.Framework == "PCI DSS")
            .CountAsync();

        if (existingCount > 0)
        {
            _logger.LogInformation("PCI DSS controls already seeded ({Count} controls)", existingCount);
            return;
        }

        _logger.LogInformation("Seeding PCI DSS compliance controls...");

        var pciDssControls = new List<ComplianceControl>
        {
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-1",
                ControlName = "Network Security Controls",
                Description = "Install and maintain network security controls",
                Category = "Network Security",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-2",
                ControlName = "Secure Configurations",
                Description = "Apply secure configurations to all system components",
                Category = "Configuration Management",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-3",
                ControlName = "Cardholder Data Protection",
                Description = "Protect stored cardholder data",
                Category = "Data Protection",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-4",
                ControlName = "Transmission Encryption",
                Description = "Protect cardholder data with strong cryptography during transmission",
                Category = "Data Protection",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-5",
                ControlName = "Malware Protection",
                Description = "Protect all systems and networks from malicious software",
                Category = "System Security",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-6",
                ControlName = "Secure Development",
                Description = "Develop and maintain secure systems and software",
                Category = "System Security",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-7",
                ControlName = "Access Restriction",
                Description = "Restrict access to system components and cardholder data by business need to know",
                Category = "Access Control",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-8",
                ControlName = "User Authentication",
                Description = "Identify users and authenticate access to system components",
                Category = "Access Control",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-9",
                ControlName = "Physical Security",
                Description = "Restrict physical access to cardholder data",
                Category = "Physical Security",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-10",
                ControlName = "Logging and Monitoring",
                Description = "Log and monitor all access to system components and cardholder data",
                Category = "Monitoring",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-11",
                ControlName = "Security Testing",
                Description = "Test security of systems and networks regularly",
                Category = "Testing",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "PCI DSS",
                ControlId = "PCI-DSS-12",
                ControlName = "Policy and Program Support",
                Description = "Support information security with organizational policies and programs",
                Category = "Governance",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.ComplianceControls.AddRangeAsync(pciDssControls);
        await context.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded {Count} PCI DSS compliance controls", pciDssControls.Count);
    }

    private async Task SeedIso27001ControlsAsync(CastellanDbContext context)
    {
        // Check if ISO 27001 controls already exist
        var existingCount = await context.ComplianceControls
            .Where(c => c.Framework == "ISO 27001")
            .CountAsync();

        if (existingCount > 0)
        {
            _logger.LogInformation("ISO 27001 controls already seeded ({Count} controls)", existingCount);
            return;
        }

        _logger.LogInformation("Seeding ISO 27001 compliance controls...");

        var iso27001Controls = new List<ComplianceControl>
        {
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-5.1",
                ControlName = "Information Security Policy",
                Description = "Policies for information security should be defined, approved, published and communicated",
                Category = "Organizational",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-5.2",
                ControlName = "Information Security Risk Management",
                Description = "Information security risk management should be applied",
                Category = "Organizational",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-6.1",
                ControlName = "Personnel Security",
                Description = "Background verification checks should be carried out on all candidates for employment",
                Category = "People",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-6.3",
                ControlName = "Information Security Awareness",
                Description = "All employees should receive appropriate awareness education and training",
                Category = "People",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-8.1",
                ControlName = "Access Management",
                Description = "Access to information and other associated assets should be restricted",
                Category = "Technology",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-8.24",
                ControlName = "Cryptography",
                Description = "Rules for the effective use of cryptography should be defined and implemented",
                Category = "Technology",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-8.8",
                ControlName = "Systems Security",
                Description = "Information systems should be protected against malware",
                Category = "Technology",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-8.20",
                ControlName = "Network Security Management",
                Description = "Networks should be managed and controlled to protect information",
                Category = "Technology",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-8.25",
                ControlName = "Application Security",
                Description = "Security principles should be applied in the development of application systems",
                Category = "Technology",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-8.9",
                ControlName = "Secure Configuration",
                Description = "Configuration of systems should be reviewed and implemented securely",
                Category = "Technology",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-8.15",
                ControlName = "Logging and Monitoring",
                Description = "Activities should be logged and log information protected and regularly reviewed",
                Category = "Technology",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-8.13",
                ControlName = "Information Backup",
                Description = "Backup copies of information should be taken and tested regularly",
                Category = "Technology",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-7.1",
                ControlName = "Physical and Environmental Security",
                Description = "Physical perimeters should be defined and used to protect areas",
                Category = "Physical",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-5.24",
                ControlName = "Incident Management",
                Description = "Information security incidents should be managed through a defined process",
                Category = "Organizational",
                Priority = "High",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "ISO 27001",
                ControlId = "ISO-5.29",
                ControlName = "Business Continuity",
                Description = "ICT readiness for business continuity should be planned and implemented",
                Category = "Organizational",
                Priority = "Medium",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.ComplianceControls.AddRangeAsync(iso27001Controls);
        await context.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded {Count} ISO 27001 compliance controls", iso27001Controls.Count);
    }

    private async Task SeedCISControlsAsync(CastellanDbContext context)
    {
        // Check if CIS Controls already exist
        var existingCount = await context.ComplianceControls
            .Where(c => c.Framework == "CIS Controls v8")
            .CountAsync();

        if (existingCount > 0)
        {
            _logger.LogInformation("CIS Controls v8 already seeded ({Count} controls)", existingCount);
            return;
        }

        _logger.LogInformation("Seeding CIS Controls v8 application compliance controls...");

        var cisControls = new List<ComplianceControl>
        {
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-1.1",
                ControlName = "Establish and Maintain Detailed Enterprise Asset Inventory",
                Description = "Application awareness of deployment environment and hardware infrastructure",
                Category = "Inventory and Control of Enterprise Assets",
                Priority = "High",
                IsUserVisible = false, // Hidden from users - Application scope
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-1.2",
                ControlName = "Establish and Maintain Software Inventory",
                Description = "Application awareness of software components and dependencies",
                Category = "Inventory and Control of Software Assets",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-2.1",
                ControlName = "Establish and Maintain a Software Inventory",
                Description = "Maintain comprehensive software component tracking and versioning",
                Category = "Inventory and Control of Software Assets",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-3.1",
                ControlName = "Establish and Maintain a Data Management Process",
                Description = "Application data classification and handling procedures",
                Category = "Data Protection",
                Priority = "Medium",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-4.1",
                ControlName = "Establish and Maintain a Secure Configuration Process",
                Description = "Application secure configuration management and hardening",
                Category = "Secure Configuration of Enterprise Assets and Software",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-5.1",
                ControlName = "Establish and Maintain an Inventory of Accounts",
                Description = "Application account management and user tracking",
                Category = "Account Management",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-5.2",
                ControlName = "Use Unique Passwords",
                Description = "Application authentication security implementation",
                Category = "Account Management",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-6.1",
                ControlName = "Establish an Access Granting Process",
                Description = "Application access control policy implementation",
                Category = "Access Control Management",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-8.1",
                ControlName = "Establish and Maintain an Audit Log Management Process",
                Description = "Application audit logging capabilities and management",
                Category = "Audit Log Management",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-8.2",
                ControlName = "Collect Audit Logs",
                Description = "Application audit log collection and analysis",
                Category = "Audit Log Management",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-11.1",
                ControlName = "Establish and Maintain a Data Recovery Process",
                Description = "Application data backup and recovery capabilities",
                Category = "Data Recovery",
                Priority = "Medium",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-12.1",
                ControlName = "Ensure Network Infrastructure is Up-to-Date",
                Description = "Application network security implementation",
                Category = "Network Infrastructure Management",
                Priority = "Medium",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "CIS Controls v8",
                ControlId = "CIS-13.1",
                ControlName = "Maintain an Inventory of Sensitive Information",
                Description = "Application sensitive data protection and classification",
                Category = "Data Protection",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.ComplianceControls.AddRangeAsync(cisControls);
        await context.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded {Count} CIS Controls v8 application compliance controls", cisControls.Count);
    }

    private async Task SeedWindowsSecurityBaselinesAsync(CastellanDbContext context)
    {
        // Check if Windows Security Baselines already exist
        var existingCount = await context.ComplianceControls
            .Where(c => c.Framework == "Windows Security Baselines")
            .CountAsync();

        if (existingCount > 0)
        {
            _logger.LogInformation("Windows Security Baselines already seeded ({Count} controls)", existingCount);
            return;
        }

        _logger.LogInformation("Seeding Windows Security Baselines application compliance controls...");

        var windowsBaselineControls = new List<ComplianceControl>
        {
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-1.1",
                ControlName = "Password Policy Configuration",
                Description = "Application enforcement of strong password requirements",
                Category = "Account Policies",
                Priority = "High",
                IsUserVisible = false, // Hidden from users - Application scope
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-1.2",
                ControlName = "Account Lockout Policy",
                Description = "Application account lockout protection implementation",
                Category = "Account Policies",
                Priority = "Medium",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-2.1",
                ControlName = "Audit Policy Configuration",
                Description = "Application audit logging implementation and coverage",
                Category = "Local Policies",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-2.2",
                ControlName = "Audit Log Retention",
                Description = "Application audit log retention and management",
                Category = "Local Policies",
                Priority = "Medium",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-3.1",
                ControlName = "User Rights Assignment",
                Description = "Application user privilege management",
                Category = "Local Policies",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-4.1",
                ControlName = "Security Options Configuration",
                Description = "Application Windows security options compliance",
                Category = "Local Policies",
                Priority = "Medium",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-5.1",
                ControlName = "Windows Firewall Configuration",
                Description = "Application Windows Firewall compatibility and integration",
                Category = "Windows Firewall",
                Priority = "Medium",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-6.1",
                ControlName = "System Integrity Protection",
                Description = "Application system integrity and tamper protection",
                Category = "System Services",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-7.1",
                ControlName = "Windows Defender Integration",
                Description = "Application Windows Defender compatibility and clean operations",
                Category = "Windows Defender",
                Priority = "Medium",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-8.1",
                ControlName = "Update Management Compatibility",
                Description = "Application compatibility with Windows Update mechanisms",
                Category = "Windows Update",
                Priority = "Medium",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-9.1",
                ControlName = "Event Log Configuration",
                Description = "Application Windows Event Log integration and parsing",
                Category = "Event Logging",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "Windows Security Baselines",
                ControlId = "WSB-9.2",
                ControlName = "Security Monitoring Configuration",
                Description = "Application security monitoring and alerting capabilities",
                Category = "Event Logging",
                Priority = "High",
                IsUserVisible = false,
                Scope = ComplianceScope.Application,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.ComplianceControls.AddRangeAsync(windowsBaselineControls);
        await context.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded {Count} Windows Security Baselines application compliance controls", windowsBaselineControls.Count);
    }

    private async Task SeedSOC2ControlsAsync(CastellanDbContext context)
    {
        // Check if SOC2 controls already exist
        var existingCount = await context.ComplianceControls
            .Where(c => c.Framework == "SOC2")
            .CountAsync();

        if (existingCount > 0)
        {
            _logger.LogInformation("SOC2 controls already seeded ({Count} controls)", existingCount);
            return;
        }

        _logger.LogInformation("Seeding SOC2 compliance controls...");

        var soc2Controls = new List<ComplianceControl>
        {
            // Common Criteria
            new()
            {
                Framework = "SOC2",
                ControlId = "CC1.1",
                ControlName = "Control Environment",
                Description = "The entity demonstrates a commitment to integrity and ethical values",
                Category = "Common Criteria",
                Priority = "High",
                IsUserVisible = true, // Visible to users - Organization scope
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "CC2.1",
                ControlName = "Information and Communication",
                Description = "The entity obtains or generates relevant quality information to support internal control",
                Category = "Common Criteria",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "CC3.1",
                ControlName = "Risk Assessment",
                Description = "The entity specifies objectives to identify and assess risks",
                Category = "Common Criteria",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "CC4.1",
                ControlName = "Monitoring Activities",
                Description = "The entity selects and develops ongoing and separate evaluations",
                Category = "Common Criteria",
                Priority = "Medium",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "CC5.1",
                ControlName = "Control Activities",
                Description = "The entity selects and develops control activities",
                Category = "Common Criteria",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "CC6.1",
                ControlName = "Logical and Physical Access Controls",
                Description = "The entity implements logical access security measures",
                Category = "Common Criteria",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "CC6.2",
                ControlName = "Prior to Issuing System Credentials",
                Description = "The entity registers and authorizes new users",
                Category = "Common Criteria",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "CC6.3",
                ControlName = "Access Modification and Removal",
                Description = "The entity removes access when appropriate",
                Category = "Common Criteria",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "CC7.1",
                ControlName = "System Operations",
                Description = "The entity monitors system components for anomalies",
                Category = "Common Criteria",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "CC7.2",
                ControlName = "System Component Monitoring",
                Description = "The entity monitors system components and operations",
                Category = "Common Criteria",
                Priority = "Medium",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "CC8.1",
                ControlName = "Change Management",
                Description = "The entity authorizes changes to system components",
                Category = "Common Criteria",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },

            // Additional Trust Service Criteria
            new()
            {
                Framework = "SOC2",
                ControlId = "A1.1",
                ControlName = "Availability",
                Description = "The entity maintains system availability",
                Category = "Availability",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "C1.1",
                ControlName = "Confidentiality",
                Description = "The entity protects confidential information",
                Category = "Confidentiality",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "PI1.1",
                ControlName = "Processing Integrity",
                Description = "The entity processes data completely, accurately, and timely",
                Category = "Processing Integrity",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Framework = "SOC2",
                ControlId = "P1.1",
                ControlName = "Privacy",
                Description = "The entity collects, uses, retains, discloses, and disposes of personal information",
                Category = "Privacy",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization,
                ApplicableSectors = "General",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.ComplianceControls.AddRangeAsync(soc2Controls);
        await context.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded {Count} SOC2 compliance controls", soc2Controls.Count);
    }
}