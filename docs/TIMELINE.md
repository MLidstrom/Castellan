# Timeline Visualization

**Castellan Timeline** provides comprehensive visual analysis of security events over time, enabling security analysts to identify patterns, trends, and anomalies in security data.

## 🎯 Overview

The Timeline visualization feature transforms temporal security data into interactive charts and statistics, helping analysts understand:

- **Event Distribution**: How security events are distributed across time periods
- **Trend Analysis**: Patterns and trends in security activity
- **Risk Assessment**: Temporal analysis of high, medium, and low risk events
- **Anomaly Detection**: Identification of unusual activity patterns
- **Incident Response**: Timeline context for security investigations

## 🚀 Quick Start

### Accessing Timeline

1. **Login to Castellan**: Navigate to `http://localhost:8080`
2. **Authentication**: Login with your admin credentials
3. **Timeline Navigation**: Click the **Timeline** icon (📊) in the left navigation menu
4. **View Data**: The timeline will load showing the past 7 days of security events

### Basic Usage

```
┌─────────────────────────────────────────────────────────────┐
│ Granularity: [Day ▼] From: [09/04/2025 00:00] To: [09/11/2025 23:59] [REFRESH] │
├─────────────────────────────────────────────────────────────┤
│ Security Events Over Time                │ Summary          │
│ ┌─────────────────────────────────────┐ │ Total Events: 245│
│ │ Sep 4  ████████████████████ 45     │ │ High Risk: 12    │
│ │ Sep 5  ████████████████ 32         │ │ Medium Risk: 89  │
│ │ Sep 6  ██████████████████████ 58   │ │ Low Risk: 144    │
│ │ Sep 7  ████████████ 28             │ │                  │
│ │ Sep 8  ██████████████████ 42       │ │ Top Event Types: │
│ │ Sep 9  ██████████████ 31           │ │ • Logon: 67      │
│ │ Sep 10 █████ 9                     │ │ • Process: 43    │
│ └─────────────────────────────────────┘ │ • FileSystem: 38 │
└─────────────────────────────────────────────────────────────┘
```

## 🎮 Features

### Interactive Timeline Controls

#### Granularity Selection
- **Minute**: High-resolution view for incident analysis (up to 1 hour range)
- **Hour**: Detailed view for daily analysis (up to 1 week range)
- **Day**: Standard view for weekly/monthly analysis (default)
- **Week**: Overview for monthly/quarterly trends
- **Month**: Long-term trend analysis for annual patterns

#### Date Range Filtering
- **From/To Pickers**: Precise datetime selection with native browser controls
- **Automatic Range Validation**: Prevents invalid date ranges
- **Timezone Support**: Local timezone handling with ISO format backend communication

#### Real-time Refresh
- **Manual Refresh**: Click refresh button to reload latest data
- **Automatic Updates**: Future enhancement for real-time data streaming
- **Loading States**: Visual feedback during data fetching operations

### Visual Components

#### Timeline Chart
```typescript
interface TimelinePoint {
  timestamp: string;  // ISO 8601 format
  count: number;      // Event count for time bucket
}
```

**Features:**
- **Proportional Bars**: Visual representation scaled to maximum count
- **Timestamp Display**: Localized time formatting for readability
- **Empty State Handling**: Clear messaging when no data is available
- **Responsive Design**: Adapts to different screen sizes

#### Summary Statistics Panel
```typescript
interface TimelineStats {
  totalEvents: number;
  highRisk: number;
  mediumRisk: number;
  lowRisk: number;
  topEventTypes: Array<{
    name: string;
    count: number;
  }>;
}
```

**Displays:**
- **Event Count Totals**: Comprehensive statistics for selected time range
- **Risk Level Breakdown**: Security event categorization by risk
- **Event Type Analysis**: Most common security event categories
- **Trend Indicators**: Future enhancement for period-over-period comparison

## 🔧 Technical Implementation

### Frontend Architecture

#### Components Structure
```
src/components/
├── TimelinePanel.tsx      # Main container component
├── TimelineChart.tsx      # Chart visualization component
└── TimelineToolbar.tsx    # Interactive controls component

src/resources/
└── Timeline.tsx           # React Admin resource wrapper
```

#### Data Flow
```
User Interaction → TimelineToolbar → TimelinePanel → DataProvider → Backend API
                                         ↓
Timeline Chart ← Timeline Data ← API Response ← Timeline Controller
```

### Backend Integration

#### API Endpoints
The Timeline UI integrates with the following backend endpoints:

```http
GET /api/timeline
  ?granularity={minute|hour|day|week|month}
  &from={ISO-8601-datetime}
  &to={ISO-8601-datetime}
  &eventTypes={comma-separated-list}
  &riskLevels={comma-separated-list}

GET /api/timeline/events
  ?timeStart={ISO-8601-datetime}
  &timeEnd={ISO-8601-datetime}
  &page={number}
  &limit={number}

GET /api/timeline/stats
  ?from={ISO-8601-datetime}
  &to={ISO-8601-datetime}

GET /api/timeline/heatmap
  ?granularity={hour|day|week}
  &from={ISO-8601-datetime}
  &to={ISO-8601-datetime}

GET /api/timeline/anomalies
  ?from={ISO-8601-datetime}
  &to={ISO-8601-datetime}
  &threshold={number}
```

#### Data Provider Methods
```typescript
// Core timeline data aggregation
getTimelineData(params: {
  granularity?: 'minute' | 'hour' | 'day' | 'week' | 'month';
  from?: string;
  to?: string;
  eventTypes?: string[];
  riskLevels?: string[];
}): Promise<{ data: TimelinePoint[], total: number }>

// Detailed event listing
getTimelineEvents(params: {
  timeStart?: string;
  timeEnd?: string;
  eventTypes?: string[];
  riskLevels?: string[];
  page?: number;
  limit?: number;
}): Promise<{ data: SecurityEvent[], total: number }>

// Summary statistics
getTimelineStats(params?: {
  from?: string;
  to?: string;
}): Promise<{ data: TimelineStats }>
```

### Error Handling

#### Frontend Error States
- **Network Errors**: "Failed to load timeline" with retry option
- **Authentication Errors**: Automatic redirect to login page
- **Validation Errors**: Clear messaging for invalid date ranges
- **Empty Data**: "No data for selected range" with suggestions

#### Loading States
- **Initial Load**: Skeleton loading animation
- **Refresh**: Loading spinner with disabled controls
- **Partial Load**: Progressive loading for large datasets

## 📊 Use Cases

### Security Incident Response

#### Timeline Analysis
```
Scenario: Suspicious activity detected at 14:30 on Sept 10
```
1. **Set Granularity**: Switch to "Hour" for detailed view
2. **Focus Time Range**: Set range to Sept 10, 12:00-16:00
3. **Analyze Spike**: Identify event count spike at 14:30
4. **Drill Down**: Use timeline events to get specific event details
5. **Correlate**: Look for related events in adjacent time periods

#### Pattern Recognition
```
Scenario: Weekly security assessment
```
1. **Week View**: Set granularity to "Day" with 7-day range
2. **Risk Analysis**: Review high/medium/low risk distribution
3. **Trend Identification**: Compare daily patterns for anomalies
4. **Report Generation**: Export timeline data for compliance reporting

### Compliance Reporting

#### Audit Preparation
```
Scenario: Monthly security audit
```
1. **Monthly Range**: Set to full month with "Day" granularity
2. **Statistics Review**: Analyze total events and risk breakdown
3. **Anomaly Detection**: Identify unusual activity patterns
4. **Documentation**: Screenshot timeline for audit documentation

## 🚀 Best Practices

### Performance Optimization

#### Efficient Date Ranges
- **Avoid Large Ranges**: Limit to relevant time periods for faster loading
- **Appropriate Granularity**: Match granularity to analysis needs
- **Cached Queries**: Repeated queries benefit from browser caching

#### Data Management
```javascript
// Good: Focused analysis
{ granularity: 'hour', from: '2025-09-10T12:00:00Z', to: '2025-09-10T18:00:00Z' }

// Avoid: Overly broad ranges
{ granularity: 'minute', from: '2025-01-01T00:00:00Z', to: '2025-12-31T23:59:59Z' }
```

### Security Considerations

#### Access Control
- **JWT Authentication**: All timeline endpoints require valid authentication
- **Permission Validation**: User permissions checked for timeline access
- **Data Isolation**: Timeline data filtered by user access rights

#### Data Privacy
- **Audit Logging**: Timeline access logged for compliance
- **Data Retention**: Timeline data subject to retention policies
- **Export Controls**: Timeline exports require additional permissions

## 🔧 Customization

### Chart Styling
Timeline chart appearance can be customized via Material-UI theme:

```typescript
// Custom theme for timeline colors
const theme = createTheme({
  palette: {
    primary: {
      main: '#1976d2', // Timeline bar color
    },
    action: {
      hover: '#f5f5f5', // Background bar color
    },
  },
});
```

### Default Settings
```typescript
// TimelinePanel default configuration
const defaultSettings = {
  granularity: 'day',
  defaultRange: 7, // days
  autoRefresh: false,
  maxDataPoints: 1000
};
```

## 🐛 Troubleshooting

### Common Issues

#### "An error occurred" Message
**Symptoms**: Timeline shows error message instead of data
**Causes**:
- Backend TimelineController not registered
- Authentication token expired
- Network connectivity issues
- Invalid date range parameters

**Solutions**:
1. Verify backend service is running: `Test-NetConnection localhost -Port 5000`
2. Check authentication: Re-login to refresh JWT token
3. Validate date range: Ensure 'from' date is before 'to' date
4. Review browser console for detailed error messages

#### "No data for selected range"
**Symptoms**: Empty timeline chart with no data message
**Causes**:
- No security events in selected time range
- Overly restrictive filters applied
- Backend data source empty

**Solutions**:
1. Expand date range to include known activity periods
2. Remove event type or risk level filters
3. Verify security event ingestion is working
4. Check Security Events page for recent data

#### Poor Performance
**Symptoms**: Slow timeline loading or browser freezing
**Causes**:
- Very large date ranges with fine granularity
- Network latency to backend services
- Browser memory limitations

**Solutions**:
1. Reduce date range scope
2. Use appropriate granularity for analysis needs
3. Clear browser cache and refresh page
4. Check network connection stability

### Debug Information

#### Browser Console Debugging
Timeline components log detailed information to browser console:

```javascript
// Enable debug logging
localStorage.setItem('debug', 'timeline:*');

// Console output examples
[TimelineDataProvider] Getting timeline data: http://localhost:5000/api/timeline?granularity=day&from=2025-09-04T00:00:00.000Z&to=2025-09-11T23:59:59.999Z
[TimelinePanel] Timeline data loaded: 245 events over 7 days
[TimelineChart] Rendering 7 data points with max count: 58
```

#### Network Request Monitoring
Monitor timeline API requests in browser Network tab:
- **Status 200**: Successful data retrieval
- **Status 401**: Authentication required (re-login)
- **Status 404**: Backend endpoint not available
- **Status 500**: Backend server error (check logs)

## 📚 Related Documentation

- **[Security Events](SECURITY_EVENTS.md)**: Understanding security event data
- **[API Reference](API.md)**: Complete backend API documentation
- **[Authentication](AUTHENTICATION_SETUP.md)**: Authentication configuration
- **[Performance](PERFORMANCE.md)**: Performance optimization guidelines
- **[Troubleshooting](TROUBLESHOOTING.md)**: General troubleshooting guide

## 🔗 Integration Points

### React Admin Resources
Timeline integrates with other Castellan resources:
- **Security Events**: Drill down from timeline to event details
- **MITRE Techniques**: View timeline filtered by ATT&CK techniques
- **System Status**: Correlate timeline with system health

### Export Integration
Timeline data can be exported via:
- **Export Service**: Backend CSV/JSON/PDF export
- **Browser Export**: Client-side data export (future enhancement)
- **API Access**: Direct API access for external tools

---

**Timeline Visualization** empowers security analysts with powerful temporal analysis capabilities, making it easier to identify patterns, investigate incidents, and maintain comprehensive security awareness across time dimensions.
