import React from 'react';
import { Skeleton, SxProps, Theme } from '@mui/material';

export type ChartType = 'pie' | 'bar' | 'area' | 'rectangular';

interface ChartSkeletonProps {
  /**
   * Type of chart skeleton to render
   * - pie: Circular skeleton for pie/donut charts
   * - bar/area/rectangular: Rectangular skeleton for bar, area, and other chart types
   */
  type?: ChartType;

  /**
   * Height of the skeleton in pixels
   * Default: 300 for rectangular, 200 for pie
   */
  height?: number;

  /**
   * Custom sx props for additional styling
   */
  sx?: SxProps<Theme>;
}

/**
 * Reusable skeleton component for charts
 * Supports different chart types with appropriate skeleton shapes
 */
export const ChartSkeleton: React.FC<ChartSkeletonProps> = ({
  type = 'pie',
  height,
  sx
}) => {
  if (type === 'pie') {
    const size = height || 200;
    return (
      <Skeleton
        variant="circular"
        width={size}
        height={size}
        sx={{ mx: 'auto', mt: 3, ...sx }}
      />
    );
  }

  // For bar, area, and rectangular charts
  const chartHeight = height || 300;
  return (
    <Skeleton
      variant="rectangular"
      height={chartHeight}
      animation="wave"
      sx={sx}
    />
  );
};
