# Enterprise-Grade Architecture

## Overview

Castellan processes Windows security events through **enterprise-grade AI/ML analysis with comprehensive parallel processing, intelligent caching, and connection pooling**, stores enriched data in a vector database with scaling architecture, maintains application and MITRE data in SQLite, and provides multiple notification channels including desktop notifications, Teams/Slack integration, and a fully functional React Admin web interface.

## Current Enterprise Features (OPERATIONAL)

- **Connection Pooling**: 15-25% I/O optimization with health monitoring and automatic failover 🎆 **LIVE** - 2/2 instances healthy, HTTP monitoring active
- **Intelligent Caching**: 30-50% performance improvement with semantic similarity detection and memory management
- **🆕 AI Embedding Cache (v0.7.0)**: 30-70% reduction in embedding API calls with hash-keyed LRU cache and stampede prevention 🎆 **LIVE**
- **🆕 Polly Resilience Patterns (v0.7.0)**: Zero cascading failures with retry, circuit breaker, timeout - 97%+ reliability 🎆 **LIVE**
- **Scaling Architecture**: Complete horizontal scaling with load balancing, event queues, and auto-scaling
- **Pipeline Stability**: Production-ready with stable background operation and comprehensive monitoring
- **MITRE Integration**: Full ATT&CK framework integration with 50+ techniques displayed in web interface
- **Configuration Management**: Complete threat intelligence provider settings with persistent storage and real-time validation 🆕 **LIVE**
- **Background Service Management**: Reliable PowerShell job-based startup with comprehensive monitoring
- **🆕 Real-time Monitoring**: SignalR-powered live system health, scan progress, and threat intelligence status
- **🆕 Dashboard Data Consolidation**: Single SignalR stream replaces 4+ REST API calls with 80%+ performance improvement
- **🆕 Malware Detection**: Signature-based malware detection with comprehensive rule management and API
- **🆕 EventLogWatcher**: Real-time Windows Event Log monitoring with sub-second latency and zero event loss
- **🆕 Multi-Model Ensemble (v0.7.0)**: LLM factory pattern with support for parallel/sequential multi-model predictions 🎆 **READY**

## AI/ML Architecture (v0.7.0)

### LLM Factory Pattern

Castellan uses a sophisticated factory pattern for creating LLM client instances, enabling both single-model and multi-model ensemble predictions:

```mermaid
flowchart TD
    A[ILlmClientFactory] --> B[Create Model-Specific Client]
    B --> C[Base LLM: OllamaLlm or OpenAILlm]
    C --> D[Layer 1: ResilientLlmClient]
    D --> E[Layer 2: StrictJsonLlmClient]
    E --> F[Layer 3: TelemetryLlmClient]
    F --> G[ILlmClient Instance]

    H[Configuration] --> I{Ensemble Enabled?}
    I -->|Yes| J[EnsembleLlmClient]
    I -->|No| K[Single Model Client]
    J --> L[Multi-Model Predictions]
    K --> M[Single Model Predictions]

    G --> J
    G --> K

    subgraph "Decorator Chain"
        C
        D
        E
        F
    end

    subgraph "Client Selection"
        I
        J
        K
    end

    style A fill:#e1f5fe,color:#000
    style B fill:#fff3e0,color:#000
    style C fill:#f3e5f5,color:#000
    style D fill:#ffebee,color:#000
    style E fill:#fff8e1,color:#000
    style F fill:#fce4ec,color:#000
    style G fill:#e8f5e8,color:#000
    style H fill:#f1f8e9,color:#000
    style I fill:#e3f2fd,color:#000
    style J fill:#ffe0b2,color:#000
    style K fill:#fff3e0,color:#000
```

### AI/ML Components

- **LLM Factory Pattern**: Creates model-specific clients with full decorator chain (Base → Resilience → StrictJson → Telemetry)
- **Multi-Model Ensemble**: Supports calling multiple models (llama3.1, mistral, gemma2) with voting/aggregation strategies
- **Voting Strategies**: Majority voting (categorical fields), weighted voting, unanimous voting
- **Confidence Aggregation**: Mean, median, min, max, weighted_mean strategies for numerical fields
- **Graceful Degradation**: Multi-level fallback when models fail or don't reach quorum
- **Statistics Tracking**: Monitors model performance, success rates, failure counts, and response times
- **Embeddings**: Supports Ollama (nomic-embed-text) and OpenAI (text-embedding-3-small) with caching and resilience
- **Vector Search**: Qdrant integration for semantic similarity search and correlation detection
- **ML.NET Clustering**: Anomaly detection with 8-feature analysis for security event correlation

### Configuration

See `docs/FACTORY_PATTERN_IMPLEMENTATION.md` and `docs/ENSEMBLE_ARCHITECTURE.md` for detailed configuration guides.

## System Architecture Diagram

```mermaid
flowchart LR
    A[Windows Event Logs] --> AA[EventLogWatcher]
    AA --> B[Castellan Worker Service]
    B --> C[Parallel AI/ML Processing]
    C --> D[Qdrant Vector Database]
    B --> K[SQLite Database]
    B --> N[Threat Intelligence Services]
    B --> R[YARA Detection Engine]
    N --> O[VirusTotal API]
    N --> P[MalwareBazaar API]
    N --> Q[AlienVault OTX API]
    R --> S[Malware Detection Rule Store]
    B --> E[Notification Services]
    E --> F[Desktop Notifications]
    E --> G[Web Admin Interface]
    E --> M[Teams/Slack Channels]
    D --> H[Vector Search & Correlation Engine]
    B --> U[Correlation Background Service]
    U --> I
    K --> L[Application & MITRE Data]
    N --> I[Threat Detection Engine]
    R --> I
    H --> I
    I --> J[Security Alerts]
    J --> E
    L --> I
    
    subgraph "Data Collection"
        A
        AA
    end
    
    subgraph "Core Processing"
        B
        C
    end
    
    subgraph "Data Storage"
        D
        K
    end
    
    subgraph "Threat Intelligence"
        N
        O
        P
        Q
    end
    
    subgraph "Malware Detection"
        R
        S
    end
    
    subgraph "Intelligence & Analysis"
        H
        L
        I
        J
    end
    
    subgraph "Notifications & Interface"
        F
        G
        M
        E
    end
    
    style A fill:#e1f5fe,color:#000
    style B fill:#f3e5f5,color:#000
    style C fill:#fff3e0,color:#000
    style D fill:#e8f5e8,color:#000
    style K fill:#e8f5e8,color:#000
    style E fill:#fce4ec,color:#000
    style F fill:#f1f8e9,color:#000
    style G fill:#f1f8e9,color:#000
    style H fill:#fff8e1,color:#000
    style L fill:#fff8e1,color:#000
    style I fill:#ffebee,color:#000
    style J fill:#ffebee,color:#000
    style M fill:#e3f2fd,color:#000
    style N fill:#fce4ec,color:#000
    style O fill:#f3e5f5,color:#000
    style P fill:#f3e5f5,color:#000
    style Q fill:#f3e5f5,color:#000
    style R fill:#ffe0b2,color:#000
    style S fill:#ffe0b2,color:#000
```

## Security Architecture

### Enterprise-Grade Authentication System

```mermaid
flowchart LR
    A[Client Request] --> B[JWT Validation]
    B --> C[Token Blacklist Check]
    C --> D[API Access]
    
    E[Login/Register] --> F[BCrypt Hashing]
    F --> G[Refresh Token]
    G --> H[Secure Session]
    
    A -.-> E
    B -.-> F
    C -.-> G
    D -.-> H
    
    subgraph "Request Flow"
        A
        B
        C
        D
    end
    
    subgraph "Authentication Flow"
        E
        F
        G
        H
    end
    
    style A fill:#e1f5fe,color:#000
    style B fill:#fff3e0,color:#000
    style C fill:#ffebee,color:#000
    style D fill:#e8f5e8,color:#000
    style E fill:#f3e5f5,color:#000
    style F fill:#fff8e1,color:#000
    style G fill:#fce4ec,color:#000
    style H fill:#f1f8e9,color:#000
```

### Security Features

- **Password Security**: BCrypt hashing with configurable work factors (4-12 rounds)
- **Token Management**: JWT with secure refresh token rotation and automatic expiration
- **Token Blacklisting**: Real-time token revocation with in-memory cache and cleanup
- **Complexity Validation**: Enforced password strength requirements (length, character types)
- **Audit Trail**: Comprehensive authentication event logging for security monitoring
- **Session Security**: Automatic token expiration, renewal, and secure refresh handling
- **Multi-layered Defense**: Multiple security checks at authentication, authorization, and session levels
- **Request Tracing**: Every API request tracked with unique correlation IDs for security incident investigation
- **Configuration Safety**: Startup validation prevents deployment with invalid security configurations

## Observability & Reliability

### Enterprise-Grade Operations

```mermaid
flowchart LR
    A[Request] --> B[Correlation ID]
    B --> C[Structured Logging]
    C --> D[Error Handling]
    D --> E[Response]
    
    F[Tracing] --> G[Performance Metrics]
    G --> H[Exception Context]
    H --> I[Audit Trail]
    
    A -.-> F
    B -.-> G
    C -.-> H
    D -.-> I
    
    subgraph "Request Processing"
        A
        B
        C
        D
        E
    end
    
    subgraph "Observability Layer"
        F
        G
        H
        I
    end
    
    style A fill:#e1f5fe,color:#000
    style B fill:#fff3e0,color:#000
    style C fill:#ffebee,color:#000
    style D fill:#e8f5e8,color:#000
    style E fill:#f3e5f5,color:#000
    style F fill:#fff8e1,color:#000
    style G fill:#fce4ec,color:#000
    style H fill:#f1f8e9,color:#000
    style I fill:#e3f2fd,color:#000
```

### Reliability Features

- **Correlation Tracking**: Unique request IDs for complete request lifecycle tracing
- **Structured Logging**: JSON-formatted logs with contextual information for analysis
- **Global Exception Handling**: Consistent error responses across all API endpoints
- **Service Validation**: Startup checks ensure all critical services are properly configured
- **Configuration Validation**: Prevents deployment with invalid system configurations
- **Performance Monitoring**: Request duration tracking and service health metrics
- **Fail-Fast Architecture**: Application stops startup on critical configuration errors
- **Request Context**: Complete request information available for debugging and security analysis

## Data Storage

### Qdrant Vector Database
- **Purpose**: AI/ML embeddings and vector similarity search
- **Data**: Event embeddings, semantic search, ML.NET correlation models
- **Location**: Docker container (localhost:6333)

### SQLite Database
- **Purpose**: Application metadata and MITRE ATT&CK techniques
- **Data**: Application inventory, security configurations, MITRE techniques, security event persistence, correlation data
- **Location**: `src/Castellan.Worker/data/castellan.db` (automatically created)
- **Schema**: Applications, MITRE techniques, security events with correlation fields, system configuration
