#!/usr/bin/env bash

# Castellan Status Check Script - Bash version of status.ps1

set -euo pipefail

# Default values
PAUSE=false
DETAILED=false

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --pause)
            PAUSE=true
            shift
            ;;
        --detailed)
            DETAILED=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --pause        Wait for user input before closing"
            echo "  --detailed     Show detailed component information"
            echo "  -h, --help     Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}Castellan Component Status Check${NC}"
echo -e "${CYAN}=================================${NC}"
echo -e "${GRAY}Timestamp: $(date '+%Y-%m-%d %H:%M:%S')${NC}"
echo ""

# Status summary associative array
declare -A status_summary=(
    ["Worker"]=false
    ["Qdrant"]=false
    ["Ollama"]=false
    ["ReactAdmin"]=false
)

# Check Castellan Worker API
echo -e "${YELLOW}Checking Castellan Worker API...${NC}"
if curl -s "http://localhost:5000/health" >/dev/null 2>&1; then
    echo -e "${GREEN}OK: Worker API is running on localhost:5000${NC}"
    status_summary["Worker"]=true
    
    if [[ "$DETAILED" == "true" ]]; then
        if stats_response=$(curl -s "http://localhost:5000/api/events/stats" 2>/dev/null); then
            # Parse JSON response (basic parsing without jq dependency)
            if echo "$stats_response" | grep -q "totalEvents"; then
                total_events=$(echo "$stats_response" | grep -o '"totalEvents":[0-9]*' | cut -d':' -f2 || echo "N/A")
                high_risk=$(echo "$stats_response" | grep -o '"highRiskCount":[0-9]*' | cut -d':' -f2 || echo "N/A")
                echo -e "${GRAY}  Total Events: ${total_events}${NC}"
                echo -e "${GRAY}  High Risk: ${high_risk}${NC}"
            fi
        fi
    fi
else
    echo -e "${RED}ERROR: Worker API is not accessible (port 5000)${NC}"
    echo -e "${YELLOW}  Run: ./scripts/start.sh${NC}"
fi

# Check Qdrant Vector Database
echo -e "\n${YELLOW}Checking Qdrant Vector Database...${NC}"
if curl -s "http://localhost:6333/collections" >/dev/null 2>&1; then
    echo -e "${GREEN}OK: Qdrant is running on localhost:6333${NC}"
    status_summary["Qdrant"]=true
    
    if collections_response=$(curl -s "http://localhost:6333/collections" 2>/dev/null); then
        # Basic JSON parsing for collections count
        collection_count=$(echo "$collections_response" | grep -o '"name"' | wc -l || echo "0")
        echo -e "${GRAY}  Collections: ${collection_count}${NC}"
        
        if [[ "$DETAILED" == "true" ]] && [[ "$collection_count" -gt 0 ]]; then
            # Extract collection names (basic parsing)
            collection_names=$(echo "$collections_response" | grep -o '"name":"[^"]*"' | cut -d'"' -f4 | head -5)
            while IFS= read -r name; do
                [[ -n "$name" ]] && echo -e "${GRAY}    - ${name}${NC}"
            done <<< "$collection_names"
        fi
    fi
else
    echo -e "${RED}ERROR: Qdrant is not accessible (port 6333)${NC}"
    echo -e "${YELLOW}  Run: docker run -d --name qdrant -p 6333:6333 qdrant/qdrant${NC}"
fi

# Check Ollama LLM Service
echo -e "\n${YELLOW}Checking Ollama LLM Service...${NC}"
if curl -s "http://localhost:11434/api/tags" >/dev/null 2>&1; then
    echo -e "${GREEN}OK: Ollama is running on localhost:11434${NC}"
    status_summary["Ollama"]=true
    
    if models_response=$(curl -s "http://localhost:11434/api/tags" 2>/dev/null); then
        model_count=$(echo "$models_response" | grep -o '"name"' | wc -l || echo "0")
        echo -e "${GRAY}  Models: ${model_count}${NC}"
        
        if [[ "$DETAILED" == "true" ]] && [[ "$model_count" -gt 0 ]]; then
            # Extract model names and sizes (first 5)
            model_info=$(echo "$models_response" | grep -o '"name":"[^"]*"' | cut -d'"' -f4 | head -5)
            while IFS= read -r model; do
                if [[ -n "$model" ]]; then
                    # Get size if available (simplified)
                    size=$(echo "$models_response" | grep -A 5 "\"name\":\"$model\"" | grep -o '"size":[0-9]*' | cut -d':' -f2 | head -1)
                    if [[ -n "$size" ]]; then
                        size_gb=$(echo "scale=2; $size / 1024 / 1024 / 1024" | bc 2>/dev/null || echo "$size")
                        echo -e "${GRAY}    - ${model} (${size_gb}GB)${NC}"
                    else
                        echo -e "${GRAY}    - ${model}${NC}"
                    fi
                fi
            done <<< "$model_info"
        fi
    fi
else
    echo -e "${RED}ERROR: Ollama is not accessible (port 11434)${NC}"
    echo -e "${YELLOW}  Install from: https://ollama.ai${NC}"
fi

# Check React Admin Interface
echo -e "\n${YELLOW}Checking React Admin Interface...${NC}"
if curl -s "http://localhost:8080" >/dev/null 2>&1; then
    echo -e "${GREEN}OK: React Admin is running on localhost:8080${NC}"
    status_summary["ReactAdmin"]=true
else
    echo -e "${RED}ERROR: React Admin is not accessible (port 8080)${NC}"
    echo -e "${YELLOW}  Run: cd castellan-admin && npm start${NC}"
fi

# Check Worker Process Details
echo -e "\n${YELLOW}Checking Worker Process...${NC}"
worker_processes=()
if worker_pids=$(pgrep -f "Castellan.Worker" 2>/dev/null); then
    for pid in $worker_pids; do
        if ps -p "$pid" >/dev/null 2>&1; then
            # Get memory usage in MB
            memory_mb=$(ps -o pid,rss --no-headers -p "$pid" 2>/dev/null | awk '{print int($2/1024)}' || echo "0")
            echo -e "${GREEN}OK: Worker Process (PID: ${pid}, Memory: ${memory_mb}MB)${NC}"
            worker_processes+=("$pid")
        fi
    done
else
    # Fallback to checking any dotnet process
    if dotnet_pids=$(pgrep dotnet 2>/dev/null); then
        echo -e "${YELLOW}WARNING: .NET processes found but Worker not confirmed${NC}"
        if [[ "$DETAILED" == "true" ]]; then
            for pid in $dotnet_pids; do
                memory_mb=$(ps -o pid,rss --no-headers -p "$pid" 2>/dev/null | awk '{print int($2/1024)}' || echo "0")
                echo -e "${GRAY}  PID: ${pid}, Memory: ${memory_mb}MB${NC}"
            done
        fi
    else
        echo -e "${RED}ERROR: No Worker process detected${NC}"
    fi
fi

# Check Docker Status
echo -e "\n${YELLOW}Checking Docker...${NC}"
if command -v docker >/dev/null 2>&1; then
    docker_version=$(docker --version 2>/dev/null || echo "unknown")
    echo -e "${GREEN}OK: Docker is installed: ${docker_version}${NC}"
    
    # Check Qdrant container
    if docker ps --filter "name=qdrant" --format "{{.Names}}" 2>/dev/null | grep -q "^qdrant$"; then
        echo -e "${GREEN}  OK: Qdrant container is running${NC}"
    else
        echo -e "${YELLOW}  WARNING: Qdrant container not running${NC}"
    fi
else
    echo -e "${RED}ERROR: Docker is not installed or not running${NC}"
fi

# Check Log Files
echo -e "\n${YELLOW}Checking Log Files...${NC}"
log_paths=(
    "$HOME/.local/share/Castellan/logs"
    "/tmp/castellan-worker.log"
    "/tmp/ollama.log"
    "logs"
    "src/Castellan.Worker/logs"
)

found_logs=false
for log_path in "${log_paths[@]}"; do
    if [[ -d "$log_path" ]]; then
        # Directory with log files
        if log_files=($(find "$log_path" -name "*.log" -type f 2>/dev/null | sort -t '-' -k2 -nr | head -1)); then
            if [[ ${#log_files[@]} -gt 0 ]]; then
                found_logs=true
                echo -e "${GREEN}OK: Found logs in: ${log_path}${NC}"
                
                if [[ "$DETAILED" == "true" ]]; then
                    latest_log="${log_files[0]}"
                    if [[ -f "$latest_log" ]]; then
                        log_size=$(du -h "$latest_log" 2>/dev/null | cut -f1 || echo "0B")
                        echo -e "${GRAY}  Latest: $(basename "$latest_log") (${log_size})${NC}"
                        
                        # Show last few lines
                        if [[ -r "$latest_log" ]]; then
                            tail -n 3 "$latest_log" 2>/dev/null | while IFS= read -r line; do
                                if echo "$line" | grep -qi "error\|fatal"; then
                                    echo -e "${RED}    ERROR: ${line}${NC}"
                                elif echo "$line" | grep -qi "warn"; then
                                    echo -e "${YELLOW}    WARNING: ${line}${NC}"
                                else
                                    # Truncate long lines
                                    truncated_line="${line:0:80}"
                                    echo -e "${GRAY}    ${truncated_line}${NC}"
                                fi
                            done
                        fi
                    fi
                fi
                break
            fi
        fi
    elif [[ -f "$log_path" ]]; then
        # Single log file
        found_logs=true
        echo -e "${GREEN}OK: Found log file: ${log_path}${NC}"
        
        if [[ "$DETAILED" == "true" ]]; then
            log_size=$(du -h "$log_path" 2>/dev/null | cut -f1 || echo "0B")
            echo -e "${GRAY}  Size: ${log_size}${NC}"
        fi
        break
    fi
done

if [[ "$found_logs" == "false" ]]; then
    echo -e "${YELLOW}WARNING: No log files found${NC}"
fi

# Status Summary
echo -e "\n${CYAN}=================================${NC}"
echo -e "${CYAN}Status Summary:${NC}"
echo -e "${CYAN}=================================${NC}"

running_count=0
total_count=${#status_summary[@]}

for component in "${!status_summary[@]}"; do
    if [[ "${status_summary[$component]}" == "true" ]]; then
        ((running_count++))
    fi
done

if [[ $running_count -eq $total_count ]]; then
    echo -e "${GREEN}OK: All components are running (${running_count}/${total_count})${NC}"
elif [[ $running_count -gt 0 ]]; then
    echo -e "${YELLOW}WARNING: Partial system running (${running_count}/${total_count})${NC}"
else
    echo -e "${RED}ERROR: No components are running${NC}"
fi

echo ""
for component in "${!status_summary[@]}"; do
    if [[ "${status_summary[$component]}" == "true" ]]; then
        echo -e "${GREEN}  OK: ${component}${NC}"
    else
        echo -e "${RED}  ERROR: ${component}${NC}"
    fi
done

# Quick Start Instructions
if [[ $running_count -lt $total_count ]]; then
    echo -e "\n${YELLOW}Quick Start:${NC}"
    echo -e "${NC}  ./scripts/start.sh${NC}"
    echo -e "${GRAY}  This will start all required components${NC}"
fi

echo -e "\n${GRAY}For detailed status, run: ./scripts/status.sh --detailed${NC}"

# Wait for user input before closing (only if --pause is specified)
if [[ "$PAUSE" == "true" ]]; then
    echo -e "\n${YELLOW}Press any key to close...${NC}"
    read -n 1 -s
fi
