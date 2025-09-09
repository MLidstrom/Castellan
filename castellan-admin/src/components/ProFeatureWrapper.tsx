import React, { ReactNode, ReactElement } from 'react';

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
  // v0.5: All resources are now enabled - no more premium restrictions
  // Simply render the resource component without any premium overlays
  return <>{resourceComponent || children}</>;
};

// Component wrapper for individual premium features within resources
export const PremiumFeatureWrapper: React.FC<PremiumFeatureWrapperProps> = ({ 
  children, 
  featureName, 
  fallbackComponent,
  showUpgradeDialog = false,
  disabled = false
}) => {
  // v0.5: All features are now enabled - no more premium restrictions
  // Simply render the children without any premium overlays or restrictions
  return <>{children}</>;
};

export default PremiumFeatureWrapper;

// Backward compatibility exports
export const ProFeatureWrapper = PremiumFeatureWrapper;
export const ProResourceWrapper = PremiumResourceWrapper;
