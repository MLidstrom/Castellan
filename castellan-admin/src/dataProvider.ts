import { fetchUtils, DataProvider } from 'react-admin';
import simpleRestProvider from 'ra-data-simple-rest';
import { mockSecurityEvents, mockComplianceReports, mockSystemStatus } from './mockData';

const httpClient = (url: string, options: any = {}) => {
  if (!options.headers) {
    options.headers = new Headers({ Accept: 'application/json' });
  }
  
  const token = localStorage.getItem('auth_token');
  if (token) {
    options.headers.set('Authorization', `Bearer ${token}`);
  }
  
  return fetchUtils.fetchJson(url, options);
};

// Enhanced mock data provider with comprehensive test data
const mockDataProvider: DataProvider = {
  getList: async (resource, params) => {
    console.log('DataProvider getList called:', resource, params);
    
    // Handle MITRE data from real API
    if (resource.startsWith('mitre/')) {
      try {
        return await realDataProvider.getList(resource, params);
      } catch (error) {
        console.error('Failed to fetch MITRE data:', error);
        return { data: [], total: 0 };
      }
    }
    
    let data: any[] = [];
    let total = 0;
    
    switch (resource) {
      case 'security-events':
        data = mockSecurityEvents;
        break;
      case 'compliance-reports':
        data = mockComplianceReports;
        break;
      case 'system-status':
        data = mockSystemStatus;
        break;
      default:
        return { data: [], total: 0 };
    }
    
    total = data.length;
    
    // Apply filters if provided
    if (params.filter) {
      Object.keys(params.filter).forEach(key => {
        if (params.filter[key]) {
          data = data.filter(item => {
            const value = item[key];
            if (typeof value === 'string') {
              return value.toLowerCase().includes(params.filter[key].toLowerCase());
            }
            return value === params.filter[key];
          });
        }
      });
    }
    
    // Apply sorting
    if (params.sort) {
      const { field, order } = params.sort;
      data.sort((a, b) => {
        const aVal = a[field];
        const bVal = b[field];
        
        if (aVal < bVal) return order === 'ASC' ? -1 : 1;
        if (aVal > bVal) return order === 'ASC' ? 1 : -1;
        return 0;
      });
    }
    
    // Apply pagination
    if (params.pagination) {
      const { page, perPage } = params.pagination;
      const start = (page - 1) * perPage;
      const end = start + perPage;
      data = data.slice(start, end);
    }
    
    return { data, total };
  },
  
  getOne: async (resource, params) => {
    console.log('DataProvider getOne called:', resource, params);
    
    // Handle MITRE data from real API
    if (resource.startsWith('mitre/')) {
      try {
        return await realDataProvider.getOne(resource, params);
      } catch (error) {
        console.error('Failed to fetch MITRE data:', error);
        throw error;
      }
    }
    
    let data: any[] = [];
    
    switch (resource) {
      case 'security-events':
        data = mockSecurityEvents;
        break;
      case 'compliance-reports':
        data = mockComplianceReports;
        break;
      case 'system-status':
        data = mockSystemStatus;
        break;
      default:
        throw new Error(`Resource ${resource} not found`);
    }
    
    const record = data.find(item => item.id === parseInt(String(params.id)));
    if (!record) {
      throw new Error(`Record ${params.id} not found in ${resource}`);
    }
    
    return { data: record };
  },
  
  getMany: async (resource, params) => {
    console.log('DataProvider getMany called:', resource, params);
    
    let data: any[] = [];
    
    switch (resource) {
      case 'security-events':
        data = mockSecurityEvents;
        break;
      case 'compliance-reports':
        data = mockComplianceReports;
        break;
      case 'system-status':
        data = mockSystemStatus;
        break;
      default:
        return { data: [] };
    }
    
    const records = data.filter(item => params.ids.includes(item.id));
    return { data: records };
  },
  
  getManyReference: async (resource, params) => {
    console.log('DataProvider getManyReference called:', resource, params);
    return { data: [], total: 0 };
  },
  
  create: (resource: string, params: any) => {
    console.log('DataProvider create called:', resource, params);
    
    // Generate a new ID
    const newId = Math.max(...mockSecurityEvents.map(e => e.id), ...mockComplianceReports.map(r => r.id), ...mockSystemStatus.map(s => s.id)) + 1;
    const newRecord = { ...params.data, id: newId };
    
    // In a real implementation, this would persist the data to the backend
    // For now, we just return the new record without modifying mock arrays
    
    return Promise.resolve({ data: newRecord });
  },
  
  update: async (resource, params) => {
    console.log('DataProvider update called:', resource, params);
    
    let data: any[] = [];
    
    switch (resource) {
      case 'security-events':
        data = mockSecurityEvents;
        break;
      case 'compliance-reports':
        data = mockComplianceReports;
        break;
      case 'system-status':
        data = mockSystemStatus;
        break;
      default:
        throw new Error(`Resource ${resource} not found`);
    }
    
    const index = data.findIndex(item => item.id === params.id);
    if (index === -1) {
      throw new Error(`Record ${params.id} not found in ${resource}`);
    }
    
    data[index] = { ...data[index], ...params.data };
    return { data: data[index] };
  },
  
  updateMany: async (resource, params) => {
    console.log('DataProvider updateMany called:', resource, params);
    return { data: [] };
  },
  
  delete: async (resource, params) => {
    console.log('DataProvider delete called:', resource, params);
    return { data: params.previousData as any };
  },
  
  deleteMany: async (resource, params) => {
    console.log('DataProvider deleteMany called:', resource, params);
    return { data: [] };
  },
};

const apiUrl = process.env.REACT_APP_CASTELLAN_API_URL || 'http://localhost:5000/api';
const realDataProvider = simpleRestProvider(apiUrl, httpClient);

// Import the real Castellan data provider
import { castellanDataProvider } from './dataProvider/castellanDataProvider';

// Switch to real API - backend is ready!
export const dataProvider = castellanDataProvider;
