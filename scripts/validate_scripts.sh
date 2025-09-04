#!/usr/bin/env bash

# Castellan Script Validator - Bash version of validate_ps.ps1
# This script validates bash scripts for syntax errors and style issues

set -euo pipefail

# Default values
BASE_DIR="scripts"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

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
        -b|--base-dir)
            BASE_DIR="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -b, --base-dir DIR    Base directory to search for scripts (default: scripts)"
            echo "  -h, --help            Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                    # Validate all scripts in the scripts directory"
            echo "  $0 -b /path/to/dir   # Validate scripts in a specific directory"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Resolve base directory path
if [[ "$BASE_DIR" = /* ]]; then
    SEARCH_DIR="$BASE_DIR"
else
    SEARCH_DIR="$(dirname "$SCRIPT_DIR")/$BASE_DIR"
fi

echo -e "${CYAN}Castellan Script Validator${NC}"
echo -e "${CYAN}==========================${NC}"
echo -e "${GRAY}Searching in: ${SEARCH_DIR}${NC}"
echo ""

# Results arrays
declare -a syntax_errors=()
declare -a style_warnings=()
declare -a valid_scripts=()

# Function to check bash syntax
check_bash_syntax() {
    local script_file=$1
    local filename=$(basename "$script_file")
    
    echo -e "${YELLOW}Checking syntax: ${filename}${NC}"
    
    # Check if file has shebang
    if ! head -1 "$script_file" | grep -q "^#!/.*bash"; then
        if head -1 "$script_file" | grep -q "^#!"; then
            style_warnings+=("$filename: Non-bash shebang detected")
        else
            style_warnings+=("$filename: Missing shebang")
        fi
    fi
    
    # Check syntax using bash -n
    if bash -n "$script_file" 2>/dev/null; then
        echo -e "${GREEN}  ✓ Syntax OK${NC}"
        valid_scripts+=("$filename")
    else
        local error_output
        error_output=$(bash -n "$script_file" 2>&1)
        echo -e "${RED}  ✗ Syntax Error${NC}"
        syntax_errors+=("$filename: $error_output")
        return 1
    fi
    
    return 0
}

# Function to perform basic style checks
check_bash_style() {
    local script_file=$1
    local filename=$(basename "$script_file")
    
    # Check for common style issues
    
    # Check for missing 'set -euo pipefail' or similar
    if ! grep -q "set -[euo]*" "$script_file"; then
        style_warnings+=("$filename: Consider adding 'set -euo pipefail' for error handling")
    fi
    
    # Check for unquoted variables (basic check)
    if grep -q '\$[A-Za-z_][A-Za-z0-9_]*[^"]' "$script_file" && \
       ! grep -q "# shellcheck disable=SC2086" "$script_file"; then
        # This is a simplified check - real shellcheck would be more comprehensive
        style_warnings+=("$filename: Potential unquoted variables detected")
    fi
    
    # Check for hardcoded paths that might not be portable
    if grep -q '\/usr\/local\/bin\|\/opt\/\|C:\\' "$script_file"; then
        style_warnings+=("$filename: Hardcoded paths detected - consider using relative paths")
    fi
    
    # Check for executable permissions
    if [[ ! -x "$script_file" ]]; then
        style_warnings+=("$filename: File is not executable (chmod +x needed)")
    fi
}

# Function to check if shellcheck is available and run it
run_shellcheck() {
    local script_file=$1
    local filename=$(basename "$script_file")
    
    if command -v shellcheck >/dev/null 2>&1; then
        echo -e "${GRAY}  Running shellcheck...${NC}"
        local shellcheck_output
        if shellcheck_output=$(shellcheck -f gcc "$script_file" 2>&1); then
            echo -e "${GREEN}  ✓ Shellcheck OK${NC}"
        else
            echo -e "${YELLOW}  ⚠ Shellcheck warnings:${NC}"
            echo -e "${GRAY}$shellcheck_output${NC}"
            # Add to style warnings (limit to first few lines to avoid spam)
            local warning_summary
            warning_summary=$(echo "$shellcheck_output" | head -3 | tr '\n' '; ')
            style_warnings+=("$filename: Shellcheck warnings: $warning_summary")
        fi
        return 0
    else
        echo -e "${GRAY}  Shellcheck not available (install with: apt-get install shellcheck)${NC}"
        return 1
    fi
}

# Main validation loop
echo -e "${YELLOW}Searching for bash scripts...${NC}"

# Find bash scripts (files with .sh extension or bash shebang)
script_files=()
if [[ -d "$SEARCH_DIR" ]]; then
    while IFS= read -r -d '' file; do
        script_files+=("$file")
    done < <(find "$SEARCH_DIR" -type f \( -name "*.sh" -o -exec grep -l "^#!/.*bash" {} \; \) -print0 2>/dev/null)
else
    echo -e "${RED}ERROR: Search directory does not exist: ${SEARCH_DIR}${NC}"
    exit 1
fi

if [[ ${#script_files[@]} -eq 0 ]]; then
    echo -e "${YELLOW}No bash scripts found in ${SEARCH_DIR}${NC}"
    exit 0
fi

echo -e "${CYAN}Found ${#script_files[@]} script(s) to validate${NC}"
echo ""

# Validate each script
for script_file in "${script_files[@]}"; do
    if [[ -r "$script_file" ]]; then
        check_bash_syntax "$script_file"
        check_bash_style "$script_file"
        run_shellcheck "$script_file"
        echo ""
    else
        syntax_errors+=("$(basename "$script_file"): File not readable")
    fi
done

# Summary
echo -e "${CYAN}Validation Summary${NC}"
echo -e "${CYAN}=================${NC}"

if [[ ${#valid_scripts[@]} -gt 0 ]]; then
    echo -e "${GREEN}Valid Scripts (${#valid_scripts[@]}):${NC}"
    for script in "${valid_scripts[@]}"; do
        echo -e "${GREEN}  ✓ ${script}${NC}"
    done
    echo ""
fi

if [[ ${#syntax_errors[@]} -gt 0 ]]; then
    echo -e "${RED}Syntax Errors (${#syntax_errors[@]}):${NC}"
    for error in "${syntax_errors[@]}"; do
        echo -e "${RED}  ✗ ${error}${NC}"
    done
    echo ""
fi

if [[ ${#style_warnings[@]} -gt 0 ]]; then
    echo -e "${YELLOW}Style Warnings (${#style_warnings[@]}):${NC}"
    for warning in "${style_warnings[@]}"; do
        echo -e "${YELLOW}  ⚠ ${warning}${NC}"
    done
    echo ""
fi

# Exit with appropriate code
if [[ ${#syntax_errors[@]} -gt 0 ]]; then
    echo -e "${RED}Validation failed: ${#syntax_errors[@]} syntax error(s) found${NC}"
    exit 1
elif [[ ${#style_warnings[@]} -gt 0 ]]; then
    echo -e "${YELLOW}Validation completed with ${#style_warnings[@]} warning(s)${NC}"
    echo -e "${GRAY}Consider addressing style warnings for better script quality${NC}"
    exit 0
else
    echo -e "${GREEN}All scripts validated successfully!${NC}"
    exit 0
fi
