import React from 'react';
import { 
  Box, 
  Typography, 
  Button,
  Chip,
  Alert,
  Container,
  Paper
} from '@mui/material';
import { 
  Lock as LockIcon,
  Star as StarIcon,
  Upgrade as UpgradeIcon,
  Security as SecurityIcon,
  Assessment as ComplianceIcon,
  BugReport as ThreatIcon
} from '@mui/icons-material';

interface DisabledPremiumResourceProps {
  resourceName: string;
  feature: string;
  description: string;
  icon?: React.ReactElement;
  benefits?: string[];
}

export const DisabledPremiumResourceComponent: React.FC<DisabledPremiumResourceProps> = ({
  resourceName,
  feature,
  description,
  icon,
  benefits = []
}) => {
  return (
    <Container maxWidth="md" sx={{ mt: 4, mb: 4 }}>
      <Paper 
        elevation={3}
        sx={{ 
          p: 4,
          textAlign: 'center',
          background: 'linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%)',
          border: '2px dashed #ccc',
          position: 'relative'
        }}
      >
        {/* Premium Badge */}
        <Box sx={{ position: 'absolute', top: 16, right: 16 }}>
          <Chip 
            icon={<StarIcon />}
            label="PREMIUM FEATURE"
            color="warning"
            variant="filled"
          />
        </Box>

        {/* Main Icon */}
        <Box sx={{ mb: 3 }}>
          <Box 
            sx={{ 
              display: 'inline-flex',
              p: 2,
              borderRadius: '50%',
              backgroundColor: 'rgba(255, 152, 0, 0.1)',
              border: '2px solid #ff9800'
            }}
          >
            {icon || <LockIcon sx={{ fontSize: 48, color: '#ff9800' }} />}
          </Box>
        </Box>

        {/* Title */}
        <Typography variant="h4" component="h1" gutterBottom sx={{ fontWeight: 'bold' }}>
          {resourceName}
        </Typography>

        {/* Description */}
        <Typography variant="h6" color="textSecondary" paragraph>
          {description}
        </Typography>

        {/* Current Edition Alert */}
        <Alert 
          severity="info" 
          sx={{ mb: 3, textAlign: 'left' }}
          icon={<SecurityIcon />}
        >
          This is a premium feature not available in the current edition.
        </Alert>

        {/* Benefits */}
        {benefits.length > 0 && (
          <Box sx={{ mb: 4, textAlign: 'left' }}>
            <Typography variant="h6" gutterBottom sx={{ textAlign: 'center' }}>
              This premium feature provides:
            </Typography>
            <Box 
              component="ul" 
              sx={{ 
                listStyle: 'none',
                padding: 0,
                maxWidth: 600,
                margin: '0 auto'
              }}
            >
              {benefits.map((benefit, index) => (
                <Box 
                  component="li" 
                  key={index}
                  sx={{ 
                    display: 'flex',
                    alignItems: 'center',
                    mb: 1,
                    p: 1,
                    borderRadius: 1,
                    backgroundColor: 'rgba(255, 255, 255, 0.5)'
                  }}
                >
                  <StarIcon sx={{ color: '#ff9800', mr: 1, fontSize: 20 }} />
                  <Typography variant="body1">{benefit}</Typography>
                </Box>
              ))}
            </Box>
          </Box>
        )}

        {/* Available Features Reminder */}
        <Box sx={{ mb: 4, textAlign: 'left' }}>
          <Typography variant="h6" gutterBottom sx={{ textAlign: 'center' }}>
            Your current edition includes:
          </Typography>
          <Box 
            component="ul" 
            sx={{ 
              listStyle: 'none',
              padding: 0,
              maxWidth: 600,
              margin: '0 auto'
            }}
          >
            {[
              'Real-time Windows Event Log monitoring',
              'AI-powered security analysis with Ollama/OpenAI',
              'Vector database integration with Qdrant',
              'Desktop notifications for threats',
              'MITRE ATT&CK framework mapping',
              'IP enrichment with MaxMind GeoIP',
              'Web admin interface (this interface)',
              'PowerShell security detection'
            ].map((feature, index) => (
              <Box 
                component="li" 
                key={index}
                sx={{ 
                  display: 'flex',
                  alignItems: 'center',
                  mb: 1,
                  p: 1,
                  borderRadius: 1,
                  backgroundColor: 'rgba(76, 175, 80, 0.1)'
                }}
              >
                <SecurityIcon sx={{ color: '#4caf50', mr: 1, fontSize: 20 }} />
                <Typography variant="body2">{feature}</Typography>
              </Box>
            ))}
          </Box>
        </Box>

        {/* Action Buttons */}
        <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center', flexWrap: 'wrap' }}>
          <Button
            variant="contained"
            color="warning"
            size="large"
            startIcon={<UpgradeIcon />}
            onClick={() => window.open('https://github.com/yourusername/castellan-ai-security', '_blank')}
            sx={{ minWidth: 200 }}
          >
            Learn More
          </Button>
          <Button
            variant="outlined"
            color="primary"
            size="large"
            startIcon={<SecurityIcon />}
            onClick={() => window.location.href = '/#/security-events'}
          >
            Back to Security Events
          </Button>
        </Box>

        {/* Footer Note */}
        <Typography variant="body2" color="textSecondary" sx={{ mt: 3 }}>
          CastellanPro is open source software providing comprehensive 
          security monitoring for individual systems and small organizations.
        </Typography>
      </Paper>
    </Container>
  );
};

// Specific disabled resource components
export const DisabledComplianceReports = () => (
  <DisabledPremiumResourceComponent
    resourceName="Compliance Reports"
    feature="complianceReports"
    description="Generate comprehensive compliance reports for regulatory frameworks"
    icon={<ComplianceIcon sx={{ fontSize: 48, color: '#ff9800' }} />}
    benefits={[
      'HIPAA compliance reports for healthcare organizations',
      'SOC 2 Type II audit preparation and continuous monitoring',
      'FedRAMP compliance for government cloud deployments',
      'GDPR data privacy compliance tracking and reporting',
      'ISO 27001 information security management assessment',
      'Gap analysis and implementation roadmaps',
      'Automated compliance scoring and recommendations',
      'Executive dashboards and audit-ready documentation'
    ]}
  />
);

export const DisabledThreatScanner = () => (
  <DisabledPremiumResourceComponent
    resourceName="Threat Scanner"
    feature="threatScanner"
    description="Advanced malware detection and comprehensive system threat scanning"
    icon={<ThreatIcon sx={{ fontSize: 48, color: '#ff9800' }} />}
    benefits={[
      'Real-time malware and backdoor detection',
      'Comprehensive file system scanning',
      'Advanced heuristic analysis for unknown threats',
      'Integration with threat intelligence feeds',
      'Automated quarantine and remediation',
      'Detailed forensic analysis and reporting',
      'Custom scan profiles and scheduling',
      'Enterprise-grade threat correlation'
    ]}
  />
);

// Factory function to create disabled resource components
export const createDisabledPremiumResource = (config: DisabledPremiumResourceProps) => {
  return () => <DisabledPremiumResourceComponent {...config} />;
};