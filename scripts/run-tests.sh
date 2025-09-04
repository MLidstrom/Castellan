#!/usr/bin/env bash

# Castellan Test Runner Script - Bash version of run-tests.ps1
# This script runs all unit tests for the Castellan project

set -euo pipefail

# Default values
CONFIGURATION="Release"
NO_BUILD=false
TEST_PROJECT="src/Castellan.Tests/Castellan.Tests.csproj"
VERBOSITY="normal"

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
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        --no-build)
            NO_BUILD=true
            shift
            ;;
        -p|--test-project)
            TEST_PROJECT="$2"
            shift 2
            ;;
        -v|--verbosity)
            VERBOSITY="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -c, --configuration CONFIG    Build configuration (default: Release)"
            echo "  --no-build                    Skip build step"
            echo "  -p, --test-project PROJECT    Test project path (default: src/Castellan.Tests/Castellan.Tests.csproj)"
            echo "  -v, --verbosity LEVEL         Verbosity level (default: normal)"
            echo "  -h, --help                    Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                           # Run all tests with default settings"
            echo "  $0 --verbosity detailed      # Run with detailed output"
            echo "  $0 --no-build               # Skip build step"
            echo "  $0 -c Debug                 # Use Debug configuration"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Resolve repository root and test project path
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

if [[ "$TEST_PROJECT" = /* ]]; then
    TEST_PROJECT_PATH="$TEST_PROJECT"
else
    TEST_PROJECT_PATH="$REPO_ROOT/$TEST_PROJECT"
fi

echo -e "${CYAN}Castellan Test Runner${NC}"
echo -e "${CYAN}====================================${NC}"
echo -e "${NC}Configuration: ${CONFIGURATION}${NC}"
echo -e "${NC}Test Project: ${TEST_PROJECT_PATH}${NC}"
echo -e "${NC}No Build: ${NO_BUILD}${NC}"
echo -e "${NC}Verbosity: ${VERBOSITY}${NC}"
echo ""

# Function to check if test project exists
test_project_exists() {
    local project_path=$1
    
    if [[ -f "$project_path" ]]; then
        return 0
    else
        echo -e "${RED}ERROR: Test project not found at ${project_path}${NC}"
        return 1
    fi
}

# Function to run tests with proper error handling
invoke_tests() {
    local project_path=$1
    local config=$2
    local skip_build=$3
    local verbosity_level=$4
    
    echo -e "${YELLOW}Running tests...${NC}"
    
    local test_args=("test" "$project_path" "--configuration" "$config" "--verbosity" "$verbosity_level")
    
    if [[ "$skip_build" == "true" ]]; then
        test_args+=("--no-build")
    fi
    
    # Display the command being run
    echo -e "${GRAY}Command: dotnet ${test_args[*]}${NC}"
    echo ""
    
    # Run the tests and capture output
    if dotnet "${test_args[@]}"; then
        return 0
    else
        return $?
    fi
}

# Main execution
echo -e "${YELLOW}Step 1: Validating test project...${NC}"
if ! test_project_exists "$TEST_PROJECT_PATH"; then
    echo -e "${RED}FAILED: Cannot proceed without valid test project${NC}"
    exit 1
fi
echo -e "${GREEN}SUCCESS: Test project found${NC}"

echo -e "\n${YELLOW}Step 2: Running unit tests...${NC}"
if test_exit_code=$(invoke_tests "$TEST_PROJECT_PATH" "$CONFIGURATION" "$NO_BUILD" "$VERBOSITY"); then
    test_exit_code=0
else
    test_exit_code=$?
fi

# Analyze results
echo -e "\n${CYAN}Test Results:${NC}"
if [[ $test_exit_code -eq 0 ]]; then
    echo -e "${GREEN}SUCCESS: All tests passed!${NC}"
    echo -e "\n${GREEN}Test execution completed successfully!${NC}"
else
    echo -e "${RED}FAILED: Some tests failed (exit code: ${test_exit_code})${NC}"
    echo -e "\n${YELLOW}Troubleshooting:${NC}"
    echo -e "${NC}   1. Check the test output above for specific failures${NC}"
    echo -e "${NC}   2. Ensure all dependencies are properly installed${NC}"
    echo -e "${NC}   3. Verify the project builds successfully before running tests${NC}"
    echo -e "${NC}   4. Check for any missing test data or configuration files${NC}"
    echo -e "${NC}   5. Run individual test methods to isolate failures${NC}"
fi

echo -e "\n${CYAN}Additional Commands:${NC}"
echo -e "${NC}   Run specific test:     dotnet test ${TEST_PROJECT_PATH} --filter 'TestMethodName'${NC}"
echo -e "${NC}   Run with coverage:     dotnet test ${TEST_PROJECT_PATH} --collect:'XPlat Code Coverage'${NC}"
echo -e "${NC}   Run in watch mode:     dotnet watch test ${TEST_PROJECT_PATH}${NC}"
echo -e "${NC}   List all tests:        dotnet test ${TEST_PROJECT_PATH} --list-tests${NC}"

exit $test_exit_code
