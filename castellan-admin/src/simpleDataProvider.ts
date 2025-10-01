import { DataProvider } from 'react-admin';
import { mockSecurityEvents, mockSystemStatus } from './mockData';

// Simplified mock data provider for development
export const dataProvider: DataProvider = {
  getList: (resource: string, params: any) => {
    let data: any[] = [];
    
    switch (resource) {
      case 'security-events':
        data = [...mockSecurityEvents];
        break;
      case 'system-status':
        data = [...mockSystemStatus];
        break;
      default:
        data = [];
    }
    
    // Apply simple filtering
    if (params.filter) {
      Object.keys(params.filter).forEach(key => {
        if (params.filter[key]) {
          data = data.filter((item: any) => {
            const value = item[key];
            if (typeof value === 'string') {
              return value.toLowerCase().includes(params.filter[key].toLowerCase());
            }
            return value === params.filter[key];
          });
        }
      });
    }
    
    const total = data.length;
    
    // Apply pagination
    if (params.pagination) {
      const { page, perPage } = params.pagination;
      const start = (page - 1) * perPage;
      const end = start + perPage;
      data = data.slice(start, end);
    }
    
    return Promise.resolve({ data, total });
  },
  
  getOne: (resource: string, params: any) => {
    let data: any[] = [];
    
    switch (resource) {
      case 'security-events':
        data = mockSecurityEvents;
        break;
      case 'system-status':
        data = mockSystemStatus;
        break;
      default:
        return Promise.reject(new Error(`Resource ${resource} not found`));
    }
    
    const record = data.find((item: any) => item.id === parseInt(String(params.id)));
    if (!record) {
      return Promise.reject(new Error(`Record ${params.id} not found in ${resource}`));
    }
    
    return Promise.resolve({ data: record });
  },
  
  getMany: (resource: string, params: any) => {
    let data: any[] = [];
    
    switch (resource) {
      case 'security-events':
        data = mockSecurityEvents;
        break;
      case 'system-status':
        data = mockSystemStatus;
        break;
      default:
        data = [];
    }
    
    const records = data.filter((item: any) => params.ids.includes(item.id));
    return Promise.resolve({ data: records });
  },
  
  getManyReference: (resource: string, params: any) => {
    return Promise.resolve({ data: [], total: 0 });
  },
  
  create: (resource: string, params: any) => {
    const newId = Date.now(); // Simple ID generation
    const newRecord = { ...params.data, id: newId };
    
    // Don't modify mock data arrays to avoid TypeScript issues
    // In a real app, you would save to a database
    
    return Promise.resolve({ data: newRecord });
  },
  
  update: (resource: string, params: any) => {
    let data: any[] = [];
    
    switch (resource) {
      case 'security-events':
        data = mockSecurityEvents;
        break;
      case 'system-status':
        data = mockSystemStatus;
        break;
      default:
        return Promise.reject(new Error(`Resource ${resource} not found`));
    }
    
    const index = data.findIndex((item: any) => item.id === params.id);
    if (index === -1) {
      return Promise.reject(new Error(`Record ${params.id} not found in ${resource}`));
    }
    
    data[index] = { ...data[index], ...params.data };
    return Promise.resolve({ data: data[index] });
  },
  
  updateMany: (resource: string, params: any) => {
    return Promise.resolve({ data: params.ids });
  },
  
  delete: (resource: string, params: any) => {
    return Promise.resolve({ data: params.previousData });
  },
  
  deleteMany: (resource: string, params: any) => {
    return Promise.resolve({ data: params.ids });
  },
};