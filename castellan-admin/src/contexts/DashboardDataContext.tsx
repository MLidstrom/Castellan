import React, { createContext, useContext, useState, useCallback } from 'react';

interface DashboardData {
  securityEvents: any[];
  systemStatus: any;
  threatScanner: any[];
  timestamp: number;
}

interface DashboardDataContextType {
  dashboardData: DashboardData | null;
  setCachedData: (data: DashboardData) => void;
  getCachedData: () => DashboardData | null;
  clearCache: () => void;
  isCacheValid: (maxAgeMs?: number) => boolean;
}

const DashboardDataContext = createContext<DashboardDataContextType | undefined>(undefined);

export const useDashboardDataContext = () => {
  const context = useContext(DashboardDataContext);
  if (context === undefined) {
    throw new Error('useDashboardDataContext must be used within a DashboardDataProvider');
  }
  return context;
};

interface DashboardDataProviderProps {
  children: React.ReactNode;
}

export const DashboardDataProvider: React.FC<DashboardDataProviderProps> = ({ children }) => {
  const [dashboardData, setDashboardData] = useState<DashboardData | null>(null);

  const setCachedData = useCallback((data: DashboardData) => {
    const cachedData = {
      ...data,
      timestamp: Date.now()
    };
    setDashboardData(cachedData);
  }, []);

  const getCachedData = useCallback(() => {
    return dashboardData;
  }, [dashboardData]);

  const clearCache = useCallback(() => {
    setDashboardData(null);
  }, []);

  const isCacheValid = useCallback((maxAgeMs: number = 30000) => { // Default 30 seconds
    if (!dashboardData) return false;
    const age = Date.now() - dashboardData.timestamp;
    return age < maxAgeMs;
  }, [dashboardData]);

  const contextValue: DashboardDataContextType = {
    dashboardData,
    setCachedData,
    getCachedData,
    clearCache,
    isCacheValid
  };

  return (
    <DashboardDataContext.Provider value={contextValue}>
      {children}
    </DashboardDataContext.Provider>
  );
};