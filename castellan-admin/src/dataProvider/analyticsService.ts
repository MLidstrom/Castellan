
import { fetchUtils } from 'react-admin';

// API Configuration
const API_URL = process.env.REACT_APP_CASTELLANPRO_API_URL || 'http://localhost:5000/api';

const httpClient = (url: string, options: fetchUtils.Options = {}) => {
    if (!options.headers) {
        options.headers = new Headers({ Accept: 'application/json' });
    }
    // Temporarily disable authentication for analytics endpoints
    // const token = localStorage.getItem('auth');
    // if (token) {
    //     (options.headers as Headers).set('Authorization', `Bearer ${token}`);
    // }
    return fetchUtils.fetchJson(url, options);
};

export interface HistoricalDataPoint {
    timestamp: string;
    value: number;
}

export interface ForecastDataPoint {
    forecast: number[];
    lowerBound: number[];
    upperBound: number[];
}

export const getTrends = async (metric: string, timeRange: string, groupBy: string): Promise<{ data: HistoricalDataPoint[] }> => {
    const url = `${API_URL}/analytics/trends?metric=${metric}&timeRange=${timeRange}&groupBy=${groupBy}`;
    const response = await httpClient(url);
    return response.json; // The backend already returns { data: [...] }
};

export const getForecast = async (metric: string, forecastPeriod: number): Promise<{ data: any }> => {
    const url = `${API_URL}/analytics/forecast?metric=${metric}&forecastPeriod=${forecastPeriod}`;
    const response = await httpClient(url);
    return response.json; // The backend already returns { data: {...} }
};
