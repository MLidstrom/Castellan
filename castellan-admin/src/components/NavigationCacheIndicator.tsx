import React, { useState, useEffect } from 'react';
import { Box, Chip, Fade } from '@mui/material';
import { FlashOn as FlashIcon } from '@mui/icons-material';

export const NavigationCacheIndicator: React.FC = () => {
  const [showIndicator, setShowIndicator] = useState(false);
  const [lastCacheHit, setLastCacheHit] = useState<string | null>(null);

  useEffect(() => {
    // Listen for cache hits in console logs
    const originalConsoleLog = console.log;
    let cacheHitTimeout: NodeJS.Timeout | null = null;

    console.log = function(...args: any[]) {
      originalConsoleLog.apply(console, args);
      
      // Check if this is a cache hit message
      const message = args.join(' ');
      if (message.includes('INSTANT NAVIGATION: Cache HIT')) {
        // Extract resource name from the message
        const resourceMatch = message.match(/for ([\w-]+)/);
        const resource = resourceMatch ? resourceMatch[1] : 'resource';
        
        setLastCacheHit(resource);
        setShowIndicator(true);
        
        // Clear previous timeout
        if (cacheHitTimeout) {
          clearTimeout(cacheHitTimeout);
        }
        
        // Hide indicator after 3 seconds
        cacheHitTimeout = setTimeout(() => {
          setShowIndicator(false);
        }, 3000);
      }
    };

    // Cleanup
    return () => {
      console.log = originalConsoleLog;
      if (cacheHitTimeout) {
        clearTimeout(cacheHitTimeout);
      }
    };
  }, []);

  return (
    <Fade in={showIndicator}>
      <Box sx={{ 
        position: 'fixed', 
        top: 80, 
        right: 20, 
        zIndex: 9999,
        pointerEvents: 'none' 
      }}>
        <Chip
          icon={<FlashIcon />}
          label={`Instant load: ${lastCacheHit}`}
          color="success"
          variant="filled"
          size="small"
          sx={{
            background: 'linear-gradient(45deg, #4caf50, #81c784)',
            color: 'white',
            fontWeight: 'bold',
            boxShadow: '0 4px 8px rgba(0,0,0,0.2)',
            '@keyframes pulse': {
              '0%': {
                transform: 'scale(1)',
                opacity: 1
              },
              '50%': {
                transform: 'scale(1.05)',
                opacity: 0.8
              },
              '100%': {
                transform: 'scale(1)',
                opacity: 1
              }
            },
            animation: 'pulse 2s ease-in-out infinite'
          }}
        />
      </Box>
    </Fade>
  );
};
