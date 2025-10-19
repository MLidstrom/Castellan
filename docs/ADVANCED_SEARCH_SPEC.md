# Advanced Search & Filtering Feature Specification v0.5.0

## Overview

This specification defined the Advanced Search & Filtering capability for Castellan v0.5.0, building upon the existing basic filters to provide comprehensive search functionality for security events.

## **IMPLEMENTATION COMPLETED**

### Phase 1 Frontend (September 12, 2025)
- **AdvancedSearchDrawer Component**: Complete responsive drawer interface with accordion-style filter sections
- **Supporting Components**: FullTextSearchInput, DateRangePicker, MultiSelectFilter, RangeSliderFilter, MitreTechniqueFilter
- **State Management**: useAdvancedSearch hook with URL synchronization and debounced API calls
- **API Service**: advancedSearchService with comprehensive error handling and export functionality
- **SecurityEvents Integration**: Seamless integration with custom toolbar, search summaries, and export options
- **TypeScript Support**: Complete type definitions for all search interactions
- **Performance**: SQLite FTS5 optimization with composite indexes for sub-2-second query response

### Phase 2 Planned
- **Analytics Dashboard**: Visual trend analysis and correlation widgets
- **Saved Searches**: Bookmark and manage frequently used search configurations
- **Advanced Correlation**: Machine learning-based event relationship analysis

## Current State Analysis

### Existing Implementation
- **Basic Filters**: eventType, riskLevel, machine, user, source 
- **Backend Support**: SecurityEventsController already accepts filter parameters
- **React Components**: Filter component with TextInput and SelectInput
- **API Structure**: GET `/api/security-events` with query parameters

### Enhancement Requirements
1. **Date Range Filtering**: Start/end date selection
2. **Multi-Select Filters**: Multiple values for eventType, riskLevel
3. **Advanced Text Search**: Full-text search across messages  
4. **Numeric Range Filters**: Confidence, correlation scores
5. **Status Filtering**: Event status (Open, In Progress, Closed)
6. **MITRE Technique Filtering**: Filter by specific ATT&CK techniques

---

## Technical Architecture

### Backend API Enhancements

#### Enhanced Query Parameters
```typescript
GET /api/security-events?
  // Existing filters (keep compatibility)
  eventType=Failed%20Login&
  riskLevel=high&
  machine=server01&
  user=admin&
  source=Security&
  
  // New advanced filters
  startDate=2025-09-01T00:00:00Z&
  endDate=2025-09-09T23:59:59Z&
  eventTypes=Failed%20Login,Privilege%20Escalation&  // Multi-select
  riskLevels=high,critical&                          // Multi-select
  search=malware%20detected&                         // Full-text search
  minConfidence=0.7&
  maxConfidence=1.0&
  minCorrelationScore=0.8&
  maxCorrelationScore=1.0&
  status=Open&
  mitreTechnique=T1110.001&
  
  // Pagination (existing)
  page=1&
  limit=25&
  sort=timestamp&
  order=desc
```

#### Response Format (unchanged)
```json
{
  "data": [SecurityEventDto[]],
  "total": 1500,
  "page": 1,
  "perPage": 25
}
```

### Frontend UI Design

#### Filter Drawer Layout
```
┌─ Advanced Filters Drawer ─────────────────────────────────┐
│                                                           │
│  🕐 Date Range                                           │
│  ┌─────────────┐  ┌─────────────┐                        │
│  │ Start Date  │  │ End Date    │                        │
│  └─────────────┘  └─────────────┘                        │
│                                                           │
│  🔍 Text Search                                          │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Search messages, descriptions...                    │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                           │
│  ⚡ Event Types (Multi-Select)                           │
│  ☐ Failed Login     ☐ Privilege Escalation              │
│  ☐ Malware Detected ☐ Network Intrusion                 │
│                                                           │
│  🎯 Risk Levels (Multi-Select)                           │
│  ☐ Critical ☐ High ☐ Medium ☐ Low                       │
│                                                           │
│  📊 Score Ranges                                          │
│  Confidence: [0.7] ──────●──── [1.0]                     │
│  Correlation: [0.8] ───●─────── [1.0]                    │
│                                                           │
│  🎭 MITRE Techniques                                      │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ T1110.001 - Password Guessing                      │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                           │
│  [Clear All] [Apply Filters]                             │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

### Component Architecture

#### React Components Structure
```
SecurityEvents/
├── SecurityEventList.tsx (existing - enhanced)
├── filters/
│   ├── AdvancedSearchDrawer.tsx     (new)
│   ├── DateRangePicker.tsx          (new)  
│   ├── MultiSelectFilter.tsx        (new)
│   ├── TextSearchInput.tsx          (new)
│   ├── RangeSliderFilter.tsx        (new)
│   └── MitreTechniqueFilter.tsx     (new)
└── hooks/
    ├── useAdvancedFilters.ts        (new)
    └── useSecurityEventSearch.ts    (new)
```

---

## Implementation Plan

### Phase 1: Backend API Enhancement (16-24h)

#### SecurityEventsController Updates
```csharp
[HttpGet]
public Task<IActionResult> GetList(
    // Existing parameters (maintain compatibility)
    [FromQuery] int page = 1,
    [FromQuery] int? limit = null,
    [FromQuery] string? eventType = null,
    [FromQuery] string? riskLevel = null,
    [FromQuery] string? machine = null,
    [FromQuery] string? user = null,
    [FromQuery] string? source = null,
    
    // New advanced search parameters
    [FromQuery] DateTime? startDate = null,
    [FromQuery] DateTime? endDate = null,
    [FromQuery] string? eventTypes = null,      // CSV: "Failed Login,Malware"
    [FromQuery] string? riskLevels = null,      // CSV: "high,critical"
    [FromQuery] string? search = null,          // Full-text search
    [FromQuery] float? minConfidence = null,
    [FromQuery] float? maxConfidence = null,
    [FromQuery] float? minCorrelationScore = null,
    [FromQuery] float? maxCorrelationScore = null,
    [FromQuery] string? status = null,
    [FromQuery] string? mitreTechnique = null)
```

#### Database/Store Enhancements
- Update `ISecurityEventStore.GetSecurityEvents()` to handle new filter criteria
- Add indexed searching for performance on timestamp, eventType, riskLevel fields
- Implement full-text search capability across message fields

### Phase 2: Frontend Components Development (32-40h)

#### 2.1 Advanced Filter Drawer (12h)
- Create `AdvancedSearchDrawer.tsx` with slide-out panel
- Integrate with existing Filter component
- State management for filter values
- Apply/Clear functionality

#### 2.2 Individual Filter Components (16h)
- **DateRangePicker**: Start/end date selection with validation
- **MultiSelectFilter**: Checkbox groups for eventType, riskLevel  
- **TextSearchInput**: Debounced search input with suggestions
- **RangeSliderFilter**: Dual-range sliders for confidence/correlation scores
- **MitreTechniqueFilter**: Autocomplete dropdown with MITRE data

#### 2.3 Hooks & State Management (8h)
- `useAdvancedFilters`: Manage filter state and URL synchronization
- `useSecurityEventSearch`: Enhanced search functionality
- Integration with react-admin's filtering system

#### 2.4 UI Integration (4h)
- Update SecurityEventList to use new filters
- Responsive design for mobile/tablet
- Loading states and error handling

---

## Performance Targets

### Response Time Goals
- **Simple Filters**: < 500ms for up to 10,000 records
- **Advanced Search**: < 2s for complex multi-filter queries  
- **Full-Text Search**: < 1s for message search across 50,000+ records
- **Date Range Queries**: < 1s for 30-day ranges

### Database Optimization
```sql
-- Proposed indexes for performance
CREATE INDEX idx_security_events_timestamp ON security_events(timestamp DESC);
CREATE INDEX idx_security_events_event_type ON security_events(event_type);  
CREATE INDEX idx_security_events_risk_level ON security_events(risk_level);
CREATE INDEX idx_security_events_composite ON security_events(timestamp DESC, risk_level, event_type);

-- Full-text search index (if using PostgreSQL)
CREATE INDEX idx_security_events_search ON security_events 
USING gin(to_tsvector('english', message || ' ' || summary));
```

### Pagination Strategy
- **Client-Side**: Tailwind Dashboard's existing pagination (25 records per page)
- **Server-Side**: Offset + Limit with total count optimization
- **Large Datasets**: Virtual scrolling for 1000+ results (future enhancement)

---

## User Experience Flow

### 1. Basic Filter Usage (Existing)
```
User selects Event Type: "Failed Login" 
→ Filter applies immediately  
→ Results update in real-time
```

### 2. Advanced Search Workflow
```
User clicks "Advanced Search" button
→ Drawer slides out from right side
→ User configures multiple filters:
  • Date range: Last 7 days
  • Event types: Failed Login + Privilege Escalation  
  • Risk levels: High + Critical
  • Confidence: > 0.8
→ User clicks "Apply Filters"
→ Results update with loading indicator
→ Filter chips show active filters above data grid
→ User can clear individual filters or "Clear All"
```

### 3. Search Integration
```
User types "malware" in search box
→ Debounced search after 300ms
→ Results highlight matching terms
→ Search suggestions appear (if available)
→ Combined with other active filters
```

---

## Testing Strategy

### Unit Tests
- [ ] Individual filter component logic
- [ ] Filter state management hooks  
- [ ] API query parameter building
- [ ] Date range validation
- [ ] Multi-select value handling

### Integration Tests  
- [ ] Complete filter workflow end-to-end
- [ ] Filter combination scenarios
- [ ] URL parameter synchronization  
- [ ] Pagination with filters applied
- [ ] Performance under load (10k+ records)

### User Acceptance Tests
- [ ] Filter application and clearing
- [ ] Search functionality across different field types
- [ ] Mobile/responsive filter interactions
- [ ] Accessibility compliance (WCAG 2.1 AA)
- [ ] Cross-browser compatibility (Chrome, Firefox, Edge)

---

## Deployment Checklist

### Backend Deployment
- [ ] Database schema updates/migrations  
- [ ] API endpoint backward compatibility verified
- [ ] Index creation on production database
- [ ] Performance monitoring setup

### Frontend Deployment  
- [ ] Component builds without errors
- [ ] Filter state persists across page refreshes
- [ ] Mobile responsiveness verified
- [ ] Bundle size impact assessment

### Documentation Updates
- [ ] API documentation with new parameters
- [ ] User guide for advanced search features  
- [ ] Developer documentation for filter components
- [ ] Performance optimization guide

---

## Success Metrics

### Functional Success
- ✅ All filter types work individually and in combination
- ✅ Search results are accurate and performant  
- ✅ Filter state persists across navigation
- ✅ UI is responsive and accessible

### Performance Success  
- ✅ 95th percentile response time < 2s
- ✅ No performance regression on existing simple filters
- ✅ Database query optimization shows measurable improvement
- ✅ Frontend bundle size increase < 100KB

### User Experience Success
- ✅ Users can find specific events quickly using advanced search
- ✅ Filter combinations work intuitively  
- ✅ Mobile experience is fully functional
- ✅ No user-reported usability issues
