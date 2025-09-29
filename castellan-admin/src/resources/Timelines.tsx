import React from 'react';
import { TimelinePanel } from '../components/TimelinePanel';

// Timeline List view - shows the main timeline interface
export const TimelineList = () => {
  return <TimelinePanel />;
};

// No need for individual timeline item views since this is an aggregate view
// The timeline resource is read-only and shows overall data visualization
