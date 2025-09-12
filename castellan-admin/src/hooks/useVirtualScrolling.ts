// Virtual Scrolling Hook for React Admin Lists
// Optimizes large dataset rendering by only rendering visible items

import { useState, useEffect, useMemo, useCallback } from 'react';
import { useListContext } from 'react-admin';

interface VirtualScrollingConfig {
  itemHeight: number; // Height of each list item in pixels
  containerHeight: number; // Height of the scrollable container
  overscan: number; // Number of items to render outside visible area
}

interface VirtualScrollingResult {
  visibleItems: any[];
  totalHeight: number;
  startIndex: number;
  endIndex: number;
  scrollTop: number;
  containerProps: {
    style: React.CSSProperties;
    onScroll: (e: React.UIEvent<HTMLDivElement>) => void;
  };
  viewportProps: {
    style: React.CSSProperties;
  };
}

export const useVirtualScrolling = (
  config: VirtualScrollingConfig
): VirtualScrollingResult => {
  const { data, total } = useListContext();
  const [scrollTop, setScrollTop] = useState(0);
  
  const {
    itemHeight,
    containerHeight,
    overscan
  } = config;

  // Calculate which items should be visible
  const { startIndex, endIndex, visibleItems } = useMemo(() => {
    if (!data || data.length === 0) {
      return {
        startIndex: 0,
        endIndex: 0,
        visibleItems: []
      };
    }

    const visibleStartIndex = Math.floor(scrollTop / itemHeight);
    const visibleEndIndex = Math.min(
      visibleStartIndex + Math.ceil(containerHeight / itemHeight),
      data.length - 1
    );

    // Add overscan items
    const startIndex = Math.max(0, visibleStartIndex - overscan);
    const endIndex = Math.min(data.length - 1, visibleEndIndex + overscan);

    const visibleItems = data.slice(startIndex, endIndex + 1);

    return {
      startIndex,
      endIndex,
      visibleItems
    };
  }, [data, scrollTop, itemHeight, containerHeight, overscan]);

  // Total height of all items
  const totalHeight = useMemo(() => {
    return (total || data?.length || 0) * itemHeight;
  }, [total, data?.length, itemHeight]);

  // Handle scroll events
  const handleScroll = useCallback((e: React.UIEvent<HTMLDivElement>) => {
    setScrollTop(e.currentTarget.scrollTop);
  }, []);

  // Container props for the scrollable area
  const containerProps = useMemo(() => ({
    style: {
      height: containerHeight,
      overflowY: 'auto' as const,
      position: 'relative' as const,
    },
    onScroll: handleScroll
  }), [containerHeight, handleScroll]);

  // Viewport props for the inner content area
  const viewportProps = useMemo(() => ({
    style: {
      height: totalHeight,
      paddingTop: startIndex * itemHeight,
      position: 'relative' as const,
    }
  }), [totalHeight, startIndex, itemHeight]);

  return {
    visibleItems,
    totalHeight,
    startIndex,
    endIndex,
    scrollTop,
    containerProps,
    viewportProps
  };
};

// Optimized pagination hook that works with virtual scrolling
export const useOptimizedPagination = () => {
  const { page, perPage, setPage, setPerPage } = useListContext();
  
  // Increase default page size for virtual scrolling
  useEffect(() => {
    if (perPage < 100) {
      setPerPage(100); // Load more items at once for better virtual scrolling
    }
  }, [perPage, setPerPage]);

  // Auto-load next page when scrolling near bottom
  const handleNearBottom = useCallback((scrollTop: number, totalHeight: number, containerHeight: number) => {
    const scrollPercentage = (scrollTop + containerHeight) / totalHeight;
    
    // Load next page when 80% scrolled
    if (scrollPercentage > 0.8) {
      // Only load if we haven't reached the end
      const currentTotal = page * perPage;
      if (currentTotal < (totalHeight / 50)) { // Assuming ~50px per item average
        setPage(page + 1);
      }
    }
  }, [page, perPage, setPage]);

  return {
    handleNearBottom,
    isOptimized: perPage >= 100
  };
};

// Performance monitoring for virtual scrolling
export const useVirtualScrollPerformance = () => {
  const [renderTime, setRenderTime] = useState(0);
  const [itemsRendered, setItemsRendered] = useState(0);

  const startMeasure = useCallback(() => {
    return performance.now();
  }, []);

  const endMeasure = useCallback((startTime: number, itemCount: number) => {
    const endTime = performance.now();
    const duration = endTime - startTime;
    
    setRenderTime(duration);
    setItemsRendered(itemCount);

    // Log performance in development
    if (process.env.NODE_ENV === 'development' && duration > 16) { // 60fps = 16ms per frame
      console.warn(`Virtual scroll render took ${duration.toFixed(2)}ms for ${itemCount} items`);
    }
  }, []);

  return {
    startMeasure,
    endMeasure,
    renderTime,
    itemsRendered,
    fps: renderTime > 0 ? Math.round(1000 / renderTime) : 0
  };
};
