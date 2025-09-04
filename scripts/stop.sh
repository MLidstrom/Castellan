#!/usr/bin/env bash

# Stop Castellan Components - Bash version of stop.ps1

set -euo pipefail

# Default values
FORCE=false
KEEP_QDRANT=true    # Changed default to true - Qdrant stays running by default
STOP_QDRANT=false   # New flag to explicitly stop Qdrant
STOP_OLLAMA=false   # New flag to explicitly stop Ollama

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Arrays to track stopped/failed components
stopped_components=()
failed_components=()

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --force)
            FORCE=true
            shift
            ;;
        --keep-qdrant)
            KEEP_QDRANT=true
            STOP_QDRANT=false
            shift
            ;;
        --stop-qdrant)
            STOP_QDRANT=true
            KEEP_QDRANT=false
            shift
            ;;
        --stop-ollama)
            STOP_OLLAMA=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --force         Force stop all processes"
            echo "  --keep-qdrant   Keep Qdrant running (default)"
            echo "  --stop-qdrant   Stop Qdrant container"
            echo "  --stop-ollama   Stop Ollama service"
            echo "  -h, --help      Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}Stopping Castellan Components${NC}"
echo -e "${CYAN}==============================${NC}"
echo ""

# Function to safely stop a process by name
stop_safe_process() {
    local process_name=$1
    local display_name=$2
    
    local pids
    if pids=$(pgrep -f "$process_name" 2>/dev/null); then
        for pid in $pids; do
            if [[ "$FORCE" == "true" ]]; then
                if kill -9 "$pid" 2>/dev/null; then
                    echo -e "${GREEN}OK: Stopped ${display_name} (PID: ${pid})${NC}"
                    stopped_components+=("$display_name")
                else
                    echo -e "${RED}ERROR: Failed to stop ${display_name} (PID: ${pid})${NC}"
                    failed_components+=("$display_name")
                fi
            else
                if kill -TERM "$pid" 2>/dev/null; then
                    echo -e "${GREEN}OK: Stopped ${display_name} (PID: ${pid})${NC}"
                    stopped_components+=("$display_name")
                else
                    echo -e "${RED}ERROR: Failed to stop ${display_name} (PID: ${pid})${NC}"
                    failed_components+=("$display_name")
                fi
            fi
        done
        return 0
    else
        echo -e "${YELLOW}WARNING: ${display_name} was not running${NC}"
        return 1
    fi
}

# Stop Worker API (attempt graceful shutdown first)
echo -e "${YELLOW}Stopping Worker API...${NC}"
if curl -s -X POST "http://localhost:5000/shutdown" >/dev/null 2>&1; then
    echo -e "${GREEN}OK: Sent graceful shutdown signal to Worker API${NC}"
    sleep 2
fi

# Stop Worker process (using multiple detection methods)
worker_stopped=false

# Method 1: Check for processes listening on Worker API port (5000)
if command -v netstat >/dev/null 2>&1; then
    worker_pids=$(netstat -tlnp 2>/dev/null | awk '$4 ~ /:5000$/ {split($7,a,"/"); print a[1]}' | grep -v '^$' || true)
    for pid in $worker_pids; do
        if [[ -n "$pid" ]] && ps -p "$pid" -o comm= 2>/dev/null | grep -q "dotnet"; then
            if [[ "$FORCE" == "true" ]]; then
                kill -9 "$pid" 2>/dev/null || true
            else
                kill -TERM "$pid" 2>/dev/null || true
            fi
            echo -e "${GREEN}OK: Stopped Worker Service (PID: ${pid})${NC}"
            stopped_components+=("Worker Service")
            worker_stopped=true
        fi
    done
elif command -v lsof >/dev/null 2>&1; then
    # Alternative method using lsof
    worker_pids=$(lsof -ti:5000 2>/dev/null || true)
    for pid in $worker_pids; do
        if [[ -n "$pid" ]] && ps -p "$pid" -o comm= 2>/dev/null | grep -q "dotnet"; then
            if [[ "$FORCE" == "true" ]]; then
                kill -9 "$pid" 2>/dev/null || true
            else
                kill -TERM "$pid" 2>/dev/null || true
            fi
            echo -e "${GREEN}OK: Stopped Worker Service (PID: ${pid})${NC}"
            stopped_components+=("Worker Service")
            worker_stopped=true
        fi
    done
fi

# Method 2: Command line detection
if [[ "$worker_stopped" == "false" ]]; then
    if stop_safe_process "Castellan.Worker" "Worker Service"; then
        worker_stopped=true
    fi
fi

# Fallback: Check all dotnet processes (only with --force)
if [[ "$worker_stopped" == "false" ]] && [[ "$FORCE" == "true" ]]; then
    dotnet_pids=$(pgrep dotnet 2>/dev/null || true)
    if [[ -n "$dotnet_pids" ]]; then
        echo -e "${YELLOW}WARNING: Using --force to stop all dotnet processes...${NC}"
        for pid in $dotnet_pids; do
            if kill -9 "$pid" 2>/dev/null; then
                echo -e "${GREEN}OK: Stopped dotnet process (PID: ${pid})${NC}"
                stopped_components+=("dotnet process")
            else
                echo -e "${RED}ERROR: Failed to stop dotnet (PID: ${pid})${NC}"
            fi
        done
    fi
elif [[ "$worker_stopped" == "false" ]]; then
    dotnet_count=$(pgrep -c dotnet 2>/dev/null || echo "0")
    if [[ "$dotnet_count" -gt 0 ]]; then
        echo -e "${GRAY}INFO: Found ${dotnet_count} dotnet process(es) but cannot confirm if they're Castellan Worker${NC}"
        echo -e "${GRAY}  Worker may have already stopped or is running in a different way${NC}"
        echo -e "${GRAY}  Use --force flag only if you're sure you want to stop all dotnet processes${NC}"
    else
        echo -e "${GRAY}INFO: No dotnet processes found - Worker appears to be stopped${NC}"
    fi
fi

# Stop React Admin
echo -e "\n${YELLOW}Stopping React Admin...${NC}"
if stop_safe_process "npm.*start.*8080" "React Admin"; then
    true  # Process stopped successfully
elif pgrep -f "node.*castellan-admin" >/dev/null 2>&1; then
    stop_safe_process "node.*castellan-admin" "React Admin"
fi

# Stop Qdrant Docker container (only if --stop-qdrant is specified)
if [[ "$STOP_QDRANT" == "true" ]] && [[ "$KEEP_QDRANT" == "false" ]]; then
    echo -e "\n${YELLOW}Stopping Qdrant container...${NC}"
    if command -v docker >/dev/null 2>&1; then
        if docker ps --filter "name=qdrant" --format "{{.Names}}" 2>/dev/null | grep -q "^qdrant$"; then
            if docker stop qdrant --time 10 >/dev/null 2>&1; then
                echo -e "${GREEN}OK: Stopped Qdrant container${NC}"
                stopped_components+=("Qdrant")
            else
                echo -e "${RED}ERROR: Failed to stop Qdrant container${NC}"
                failed_components+=("Qdrant")
            fi
        else
            echo -e "${YELLOW}WARNING: Qdrant container was not running${NC}"
        fi
    else
        echo -e "${YELLOW}WARNING: Docker not available or Qdrant not running${NC}"
    fi
else
    echo -e "\n${GRAY}INFO: Keeping Qdrant running (use --stop-qdrant to stop it)${NC}"
fi

# Stop Ollama service (only if --stop-ollama is specified)
if [[ "$STOP_OLLAMA" == "true" ]]; then
    echo -e "\n${YELLOW}Stopping Ollama service...${NC}"
    if stop_safe_process "ollama" "Ollama"; then
        true  # Process stopped successfully
    fi
else
    echo -e "\n${GRAY}INFO: Keeping Ollama running (use --stop-ollama to stop it)${NC}"
fi

# Kill any orphaned processes running npm (if --force)
if [[ "$FORCE" == "true" ]]; then
    echo -e "\n${YELLOW}Cleaning up orphaned processes...${NC}"
    orphaned_pids=$(pgrep -f "npm.*castellan" 2>/dev/null || true)
    for pid in $orphaned_pids; do
        if kill -9 "$pid" 2>/dev/null; then
            echo -e "${GREEN}OK: Stopped orphaned npm process (PID: ${pid})${NC}"
        fi
    done
fi

# Summary
echo -e "\n${CYAN}==============================${NC}"
echo -e "${CYAN}Shutdown Summary:${NC}"
echo -e "${CYAN}==============================${NC}"

if [[ ${#stopped_components[@]} -gt 0 ]]; then
    echo -e "${GREEN}OK: Successfully stopped:${NC}"
    printf '%s\n' "${stopped_components[@]}" | sort -u | while read -r component; do
        echo -e "${GREEN}  - ${component}${NC}"
    done
fi

if [[ ${#failed_components[@]} -gt 0 ]]; then
    echo -e "\n${RED}ERROR: Failed to stop:${NC}"
    printf '%s\n' "${failed_components[@]}" | sort -u | while read -r component; do
        echo -e "${RED}  - ${component}${NC}"
    done
    echo -e "\n${YELLOW}Try running with --force flag to force stop all processes${NC}"
fi

if [[ ${#stopped_components[@]} -eq 0 ]] && [[ ${#failed_components[@]} -eq 0 ]]; then
    echo -e "${YELLOW}No Castellan components were running${NC}"
fi

echo -e "\n${CYAN}Castellan shutdown complete${NC}"
echo -e "${GRAY}Run ./scripts/status.sh to verify all components are stopped${NC}"
