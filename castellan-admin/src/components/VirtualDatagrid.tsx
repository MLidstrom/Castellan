// Virtual Scrolling Datagrid for React Admin
// Optimizes rendering of large datasets by only showing visible rows

import React, { useMemo, useCallback } from 'react';
import { 
  useListContext, 
  RecordContextProvider,
  useResourceContext 
} from 'react-admin';
import { 
  Table, 
  TableHead, 
  TableBody, 
  TableRow, 
  TableCell, 
  Box,
  Typography 
} from '@mui/material';
import { 
  useVirtualScrolling, 
  useOptimizedPagination, 
  useVirtualScrollPerformance 
} from '../hooks/useVirtualScrolling';

interface VirtualDatagridProps {
  children: React.ReactNode;
  rowHeight?: number;
  containerHeight?: number;
  overscan?: number;
}

const VirtualDatagrid: React.FC<VirtualDatagridProps> = ({
  children,
  rowHeight = 60,
  containerHeight = 600,
  overscan = 5
}) => {
  const { data, total, isLoading } = useListContext();
  const resource = useResourceContext();
  
  // Performance monitoring
  const { startMeasure, endMeasure, renderTime, itemsRendered, fps } = useVirtualScrollPerformance();
  
  // Virtual scrolling configuration
  const virtualScrollConfig = useMemo(() => ({
    itemHeight: rowHeight,
    containerHeight,
    overscan
  }), [rowHeight, containerHeight, overscan]);
  
  // Virtual scrolling hook
  const {
    visibleItems,
    totalHeight,
    startIndex,
    endIndex,
    scrollTop,
    containerProps,
    viewportProps
  } = useVirtualScrolling(virtualScrollConfig);
  
  // Optimized pagination
  const { handleNearBottom, isOptimized } = useOptimizedPagination();
  
  // Enhanced scroll handler with pagination
  const handleScroll = useCallback((e: React.UIEvent<HTMLDivElement>) => {
    containerProps.onScroll(e);
    handleNearBottom(e.currentTarget.scrollTop, totalHeight, containerHeight);
  }, [containerProps.onScroll, handleNearBottom, totalHeight, containerHeight]);
  
  // Clone children to get header structure
  const headerChildren = useMemo(() => {
    return React.Children.map(children, (child) => {
      if (React.isValidElement(child)) {
        return React.cloneElement(child as React.ReactElement<any>, { 
          record: undefined, // No record for header
          isHeader: true 
        });
      }
      return child;
    });
  }, [children]);
  
  // Render visible rows
  const renderVisibleRows = useCallback(() => {
    const startTime = startMeasure();
    
    const rows = visibleItems.map((record, index) => {
      const actualIndex = startIndex + index;
      
      return (
        <RecordContextProvider value={record} key={record.id || actualIndex}>
          <TableRow 
            hover
            sx={{ 
              height: rowHeight,
              position: 'absolute',
              top: index * rowHeight,
              width: '100%',
              display: 'flex'
            }}
          >
            {React.Children.map(children, (child, cellIndex) => {
              if (React.isValidElement(child)) {
                return (
                  <TableCell 
                    key={cellIndex}
                    sx={{ 
                      flex: 1,
                      display: 'flex',
                      alignItems: 'center',
                      borderBottom: '1px solid rgba(224, 224, 224, 1)'
                    }}
                  >
                    {React.cloneElement(child as React.ReactElement<any>, { record })}
                  </TableCell>
                );
              }
              return null;
            })}
          </TableRow>
        </RecordContextProvider>
      );
    });
    
    endMeasure(startTime, visibleItems.length);
    return rows;
  }, [visibleItems, startIndex, children, rowHeight, startMeasure, endMeasure]);
  
  if (isLoading) {
    return (
      <Box sx={{ 
        height: containerHeight, 
        display: 'flex', 
        alignItems: 'center', 
        justifyContent: 'center' 
      }}>
        <Typography>Loading...</Typography>
      </Box>
    );
  }
  
  if (!data || data.length === 0) {
    return (
      <Box sx={{ 
        height: containerHeight, 
        display: 'flex', 
        alignItems: 'center', 
        justifyContent: 'center' 
      }}>
        <Typography>No data available</Typography>
      </Box>
    );
  }
  
  return (
    <Box>
      {/* Performance indicator (development only) */}
      {process.env.NODE_ENV === 'development' && (
        <Box sx={{ 
          display: 'flex', 
          gap: 2, 
          mb: 1, 
          fontSize: '0.75rem', 
          color: 'text.secondary',
          alignItems: 'center'
        }}>
          <span>📊 Virtual Scroll Stats:</span>
          <span>{itemsRendered}/{total || data.length} items rendered</span>
          <span>{renderTime.toFixed(1)}ms render time</span>
          <span>{fps} FPS</span>
          {isOptimized && <span>✅ Optimized pagination</span>}
        </Box>
      )}
      
      {/* Virtual scrolling container */}
      <div {...containerProps} onScroll={handleScroll}>
        <Table sx={{ tableLayout: 'fixed', width: '100%' }}>
          {/* Fixed header */}
          <TableHead sx={{ 
            position: 'sticky', 
            top: 0, 
            zIndex: 1, 
            backgroundColor: 'background.paper',
            borderBottom: '2px solid rgba(224, 224, 224, 1)'
          }}>
            <TableRow sx={{ display: 'flex', height: rowHeight }}>
              {React.Children.map(headerChildren, (child, index) => (
                <TableCell 
                  key={index} 
                  sx={{ 
                    flex: 1, 
                    fontWeight: 'bold',
                    display: 'flex',
                    alignItems: 'center'
                  }}
                >
                  {child}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          
          {/* Virtual viewport */}
          <TableBody>
            <tr>
              <td colSpan={React.Children.count(children)}>
                <div {...viewportProps}>
                  <div style={{ position: 'relative' }}>
                    {renderVisibleRows()}
                  </div>
                </div>
              </td>
            </tr>
          </TableBody>
        </Table>
      </div>
      
      {/* Footer info */}
      <Box sx={{ 
        mt: 1, 
        display: 'flex', 
        justifyContent: 'space-between', 
        alignItems: 'center',
        fontSize: '0.875rem',
        color: 'text.secondary'
      }}>
        <span>
          Showing {Math.min(endIndex - startIndex + 1, visibleItems.length)} of {total || data.length} {resource} records
        </span>
        <span>
          Scroll position: {Math.round((scrollTop / (totalHeight - containerHeight)) * 100)}%
        </span>
      </Box>
    </Box>
  );
};

export default VirtualDatagrid;
