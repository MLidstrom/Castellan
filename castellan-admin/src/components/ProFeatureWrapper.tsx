import React, { ReactNode, ReactElement } from 'react';
import { 
  Box, 
  Typography, 
  Card, 
  CardContent, 
  Button,
  Chip,
  Alert,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  IconButton,
  Tooltip
} from '@mui/material';
import { 
  Lock as LockIcon,
  Info as InfoIcon,
  Close as CloseIcon,
  Star as StarIcon,
  Upgrade as UpgradeIcon
} from '@mui/icons-material';

interface PremiumFeatureWrapperProps {
  children: ReactNode;
  featureName: string;
  fallbackComponent?: ReactNode;
  showUpgradeDialog?: boolean;
  disabled?: boolean;
}

// Resource wrapper that disables entire resources in react-admin
export const PremiumResourceWrapper: React.FC<PremiumFeatureWrapperProps & { 
  resourceComponent: ReactElement;
  resourceName: string;
}> = ({ 
  children, 
  featureName, 
  resourceComponent, 
  resourceName, 
  fallbackComponent,
  showUpgradeDialog = true 
}) => {
  const [upgradeDialogOpen, setUpgradeDialogOpen] = React.useState(false);

  // Always show disabled version for CastellanProFree
  const upgradeMessage = `${featureName} is a premium feature not available in the current edition.`;

  return (
    <>
      {/* Disabled Resource Card */}
      <Card 
        sx={{ 
          position: 'relative',
          opacity: 0.6,
          backgroundColor: '#f5f5f5',
          border: '2px dashed #ccc',
          '&:hover': {
            opacity: 0.8,
          }
        }}
      >
        {/* Premium Badge */}
        <Box sx={{ position: 'absolute', top: 8, right: 8, zIndex: 1 }}>
          <Chip 
            icon={<StarIcon />}
            label="PREMIUM"
            color="warning"
            size="small"
            variant="filled"
          />
        </Box>

        {/* Lock Overlay */}
        <Box 
          sx={{ 
            position: 'absolute',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: 'rgba(255, 255, 255, 0.8)',
            zIndex: 2,
            cursor: 'pointer'
          }}
          onClick={() => showUpgradeDialog && setUpgradeDialogOpen(true)}
        >
          <Box sx={{ textAlign: 'center', p: 2 }}>
            <LockIcon sx={{ fontSize: 48, color: 'text.secondary', mb: 1 }} />
            <Typography variant="h6" color="textSecondary" gutterBottom>
              {resourceName} - Premium Feature
            </Typography>
            <Typography variant="body2" color="textSecondary" sx={{ mb: 2 }}>
              This is a premium feature
            </Typography>
            {showUpgradeDialog && (
              <Button
                variant="outlined"
                color="warning"
                startIcon={<UpgradeIcon />}
                size="small"
              >
                Learn More
              </Button>
            )}
          </Box>
        </Box>

        {/* Grayed out content */}
        <CardContent sx={{ filter: 'grayscale(100%)', pointerEvents: 'none' }}>
          {fallbackComponent || (
            <Box sx={{ p: 4 }}>
              <Typography variant="h6" color="textSecondary">
                {resourceName}
              </Typography>
              <Typography variant="body2" color="textSecondary">
                {upgradeMessage}
              </Typography>
            </Box>
          )}
        </CardContent>
      </Card>

      {/* Upgrade Dialog */}
      <Dialog 
        open={upgradeDialogOpen} 
        onClose={() => setUpgradeDialogOpen(false)}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <StarIcon color="warning" />
          Premium Feature
          <Box sx={{ flexGrow: 1 }} />
          <IconButton onClick={() => setUpgradeDialogOpen(false)}>
            <CloseIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent>
          <Alert severity="info" sx={{ mb: 2 }}>
            This is a premium feature not available in the current edition.
          </Alert>
          
          <Typography variant="h6" gutterBottom>
            {resourceName}
          </Typography>
          <Typography variant="body1" paragraph>
            {upgradeMessage}
          </Typography>
          
          <Box sx={{ mt: 2 }}>
            <Typography variant="h6" gutterBottom>
              Available features include:
            </Typography>
            <ul style={{ margin: 0, paddingLeft: '1.5rem' }}>
              <li>Windows Event Log monitoring</li>
              <li>AI-powered security analysis</li>
              <li>Desktop notifications</li>
              <li>Real-time threat detection</li>
              <li>Vector database integration</li>
              <li>MITRE ATT&CK mapping</li>
              <li>IP enrichment with GeoIP</li>
              <li>Web admin interface</li>
            </ul>
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setUpgradeDialogOpen(false)}>
            Close
          </Button>
          <Button 
            variant="contained" 
            color="warning" 
            startIcon={<InfoIcon />}
            onClick={() => {
              window.open('https://github.com/yourusername/castellan-ai-security', '_blank');
            }}
          >
            Learn More
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
};

// Component wrapper for individual premium features within resources
export const PremiumFeatureWrapper: React.FC<PremiumFeatureWrapperProps> = ({ 
  children, 
  featureName, 
  fallbackComponent,
  showUpgradeDialog = false,
  disabled = false
}) => {
  const [upgradeDialogOpen, setUpgradeDialogOpen] = React.useState(false);

  // If explicitly disabled, render normally
  if (disabled) {
    return <>{children}</>;
  }

  // Always show disabled state for premium features in CastellanProFree
  const upgradeMessage = `${featureName} is a premium feature not available in the current edition.`;

  return (
    <>
      <Box 
        sx={{ 
          position: 'relative',
          opacity: 0.5,
          filter: 'grayscale(100%)',
          pointerEvents: 'none',
        }}
      >
        {/* Premium indicator */}
        <Tooltip title={upgradeMessage}>
          <Chip 
            icon={<LockIcon />}
            label="PREMIUM"
            color="warning"
            size="small"
            sx={{ 
              position: 'absolute', 
              top: 4, 
              right: 4, 
              zIndex: 1,
              pointerEvents: 'auto',
              cursor: showUpgradeDialog ? 'pointer' : 'default'
            }}
            onClick={() => showUpgradeDialog && setUpgradeDialogOpen(true)}
          />
        </Tooltip>

        {fallbackComponent || children}
      </Box>

      {/* Upgrade Dialog */}
      {showUpgradeDialog && (
        <Dialog 
          open={upgradeDialogOpen} 
          onClose={() => setUpgradeDialogOpen(false)}
          maxWidth="sm"
          fullWidth
        >
          <DialogTitle>
            Premium Feature
          </DialogTitle>
          <DialogContent>
            <Typography variant="body1">
              {upgradeMessage}
            </Typography>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setUpgradeDialogOpen(false)}>
              Close
            </Button>
            <Button 
              variant="contained" 
              color="warning" 
              onClick={() => {
                window.open('https://github.com/yourusername/castellan-ai-security', '_blank');
                setUpgradeDialogOpen(false);
              }}
            >
              Learn More
            </Button>
          </DialogActions>
        </Dialog>
      )}
    </>
  );
};

export default PremiumFeatureWrapper;

// Backward compatibility exports
export const ProFeatureWrapper = PremiumFeatureWrapper;
export const ProResourceWrapper = PremiumResourceWrapper;