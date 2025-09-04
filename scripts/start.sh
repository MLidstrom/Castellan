#!/usr/bin/env bash

# Start Castellan - Bash version of start.ps1
# Enhanced wrapper with validation and error handling

set -euo pipefail

# Default values
NO_BUILD=false
BACKGROUND=false

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
        --no-build)
            NO_BUILD=true
            shift
            ;;
        --background)
            BACKGROUND=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --no-build     Skip build step"
            echo "  --background   Run Worker in background"
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

echo -e "${CYAN}Starting Castellan Worker Service...${NC}"
echo -e "${GRAY}The Worker will automatically start all required services.${NC}"
echo ""

# Function to check if .NET is installed
check_dotnet_installed() {
    if command -v dotnet >/dev/null 2>&1; then
        local dotnet_version
        dotnet_version=$(dotnet --version 2>/dev/null || echo "unknown")
        echo -e "${GREEN}OK: .NET SDK found: ${dotnet_version}${NC}"
        return 0
    else
        echo -e "${RED}ERROR: .NET SDK not found. Please install .NET 8.0 or later from https://dotnet.microsoft.com/download${NC}"
        return 1
    fi
}

# Function to validate project exists
test_project_exists() {
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local project_path="${script_dir}/../src/Castellan.Worker/Castellan.Worker.csproj"
    
    if [[ -f "$project_path" ]]; then
        echo -e "${GREEN}OK: Worker project found${NC}"
        return 0
    else
        echo -e "${RED}ERROR: Worker project not found at: ${project_path}${NC}"
        return 1
    fi
}

# Function to build the project
build_project() {
    echo -e "\n${YELLOW}Building project...${NC}"
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local worker_dir="${script_dir}/../src/Castellan.Worker"
    
    if pushd "$worker_dir" >/dev/null; then
        if dotnet build --configuration Release; then
            echo -e "${GREEN}OK: Build successful${NC}"
            popd >/dev/null
            return 0
        else
            echo -e "${RED}ERROR: Build failed${NC}"
            popd >/dev/null
            return 1
        fi
    else
        echo -e "${RED}ERROR: Could not change to worker directory${NC}"
        return 1
    fi
}

# Function to start the Worker service
start_worker() {
    local run_in_background=$1
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local worker_path="${script_dir}/../src/Castellan.Worker"
    
    if [[ "$run_in_background" == "true" ]]; then
        echo -e "\n${YELLOW}Starting Worker in background...${NC}"
        if pushd "$worker_path" >/dev/null; then
            nohup dotnet run > /tmp/castellan-worker.log 2>&1 &
            local pid=$!
            popd >/dev/null
            echo -e "${GREEN}OK: Worker started in background (PID: ${pid})${NC}"
            echo -e "${GRAY}  Use './scripts/stop.sh' to stop all services${NC}"
            echo -e "${GRAY}  Use './scripts/status.sh' to check service status${NC}"
            echo -e "${GRAY}  Logs: /tmp/castellan-worker.log${NC}"
            return 0
        else
            echo -e "${RED}ERROR: Could not change to worker directory${NC}"
            return 1
        fi
    else
        echo -e "\n${YELLOW}Starting Worker in foreground...${NC}"
        echo -e "${GRAY}Press Ctrl+C to stop${NC}"
        if pushd "$worker_path" >/dev/null; then
            dotnet run
            local exit_code=$?
            popd >/dev/null
            
            if [[ $exit_code -ne 0 ]]; then
                echo -e "${RED}ERROR: Worker exited with code: ${exit_code}${NC}"
                return 1
            fi
            return 0
        else
            echo -e "${RED}ERROR: Could not change to worker directory${NC}"
            return 1
        fi
    fi
}

# Main execution flow
echo -e "${CYAN}Performing startup checks...${NC}"

# Step 1: Check and start prerequisites (Qdrant and Ollama)
echo -e "\n${CYAN}Checking prerequisites...${NC}"

# Check Qdrant
echo -e "${YELLOW}Checking Qdrant vector database...${NC}"
if command -v docker >/dev/null 2>&1; then
    if docker ps --filter "name=qdrant" --format "{{.Names}}" 2>/dev/null | grep -q "^qdrant$"; then
        echo -e "${GREEN}OK: Qdrant is already running${NC}"
    else
        echo -e "${YELLOW}Starting Qdrant container...${NC}"
        if docker start qdrant >/dev/null 2>&1; then
            echo -e "${GREEN}OK: Started existing Qdrant container${NC}"
        else
            echo -e "${YELLOW}Creating new Qdrant container...${NC}"
            if docker run -d --name qdrant -p 6333:6333 qdrant/qdrant >/dev/null 2>&1; then
                echo -e "${GREEN}OK: Created and started new Qdrant container${NC}"
            else
                echo -e "${YELLOW}WARNING: Failed to start Qdrant - vector search may not work${NC}"
            fi
        fi
    fi
else
    echo -e "${YELLOW}WARNING: Docker not available - Qdrant will not be started${NC}"
fi

# Check Ollama
echo -e "${YELLOW}Checking Ollama LLM service...${NC}"
if curl -s "http://localhost:11434/api/tags" >/dev/null 2>&1; then
    echo -e "${GREEN}OK: Ollama is already running${NC}"
else
    echo -e "${YELLOW}Starting Ollama service...${NC}"
    if command -v ollama >/dev/null 2>&1; then
        nohup ollama serve > /tmp/ollama.log 2>&1 &
        local ollama_pid=$!
        echo -e "${GREEN}OK: Started Ollama service (PID: ${ollama_pid})${NC}"
        # Wait a moment for Ollama to initialize
        sleep 3
    else
        echo -e "${YELLOW}WARNING: Ollama not installed or not accessible - AI analysis may not work${NC}"
    fi
fi

# Step 2: Validate .NET installation
if ! check_dotnet_installed; then
    exit 1
fi

# Step 3: Validate project exists
if ! test_project_exists; then
    exit 1
fi

# Step 4: Optionally build the project
if [[ "$NO_BUILD" == "false" ]]; then
    if ! build_project; then
        echo -e "\n${RED}Startup failed due to build errors${NC}"
        exit 1
    fi
else
    echo -e "${YELLOW}WARNING: Skipping build (--no-build specified)${NC}"
fi

# Step 5: Start the Worker service
if start_worker "$BACKGROUND"; then
    if [[ "$BACKGROUND" == "false" ]]; then
        echo -e "\n${CYAN}Worker service stopped${NC}"
    fi
    exit 0
else
    echo -e "\n${RED}Failed to start Castellan Worker${NC}"
    echo -e "${YELLOW}Check the error messages above for details${NC}"
    exit 1
fi

# Note: All service orchestration is handled by StartupOrchestratorService.cs
