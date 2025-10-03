import React from 'react';
import { Box, Skeleton } from '@mui/material';

interface MetricCardSkeletonProps {
  /**
   * Animation delay in seconds for staggered loading effect
   * @default 0
   */
  delay?: number;
}

/**
 * Reusable skeleton component for metric cards
 * Used in dashboard KPI cards (Security Events, System Health, Threat Scans)
 */
export const MetricCardSkeleton: React.FC<MetricCardSkeletonProps> = ({ delay = 0 }) => (
  <Box>
    <Skeleton
      variant="text"
      width="60%"
      height={48}
      sx={{ mb: 0.5, animationDelay: `${delay}s` }}
    />
    <Skeleton
      variant="text"
      width="40%"
      sx={{ animationDelay: `${delay + 0.05}s` }}
    />
  </Box>
);
