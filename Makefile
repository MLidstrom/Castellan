# Castellan Cross-Platform Development Makefile
# Supports both Windows (PowerShell) and Unix-like systems (Bash)

# Detect OS and set appropriate shell/script extensions
ifeq ($(OS),Windows_NT)
	DETECTED_OS := Windows
	SHELL_EXT := ps1
	SHELL_CMD := powershell.exe -ExecutionPolicy Bypass -File
	PATH_SEP := \\
else
	DETECTED_OS := $(shell uname -s)
	SHELL_EXT := sh
	SHELL_CMD := bash
	PATH_SEP := /
endif

# Base directories
SCRIPTS_DIR := scripts
SRC_DIR := src
ADMIN_DIR := castellan-admin

# Script paths based on OS
SCRIPT_START := $(SCRIPTS_DIR)$(PATH_SEP)start.$(SHELL_EXT)
SCRIPT_STOP := $(SCRIPTS_DIR)$(PATH_SEP)stop.$(SHELL_EXT)
SCRIPT_STATUS := $(SCRIPTS_DIR)$(PATH_SEP)status.$(SHELL_EXT)
SCRIPT_TEST := $(SCRIPTS_DIR)$(PATH_SEP)run-tests.$(SHELL_EXT)
SCRIPT_VALIDATE := $(SCRIPTS_DIR)$(PATH_SEP)validate_ps.$(SHELL_EXT)
ifeq ($(DETECTED_OS),Windows)
	SCRIPT_VALIDATE := $(SCRIPTS_DIR)$(PATH_SEP)validate_ps.$(SHELL_EXT)
else
	SCRIPT_VALIDATE := $(SCRIPTS_DIR)$(PATH_SEP)validate_scripts.$(SHELL_EXT)
endif

.PHONY: help build test start stop status validate clean install-deps setup-env

# Default target
help:
	@echo "Castellan Development Makefile ($(DETECTED_OS))"
	@echo ""
	@echo "Available targets:"
	@echo "  help         - Show this help message"
	@echo "  build        - Build the entire solution"
	@echo "  build-worker - Build only the Worker service"
	@echo "  build-admin  - Build only the React admin interface"
	@echo "  test         - Run all tests"
	@echo "  test-verbose - Run tests with detailed output"
	@echo "  test-no-build- Run tests without building"
	@echo "  start        - Start all services (background by default)"
	@echo "  start-fg     - Start all services (foreground)"
	@echo "  start-quick  - Start without building"
	@echo "  stop         - Stop all services gracefully"
	@echo "  stop-force   - Force stop all services"
	@echo "  stop-all     - Stop all services including Qdrant and Ollama"
	@echo "  status       - Check status of all components"
	@echo "  status-detail- Show detailed component status"
	@echo "  validate     - Validate all scripts"
	@echo "  clean        - Clean build artifacts"
	@echo "  install-deps - Install development dependencies"
	@echo "  setup-env    - Setup development environment"
	@echo ""
	@echo "Environment:"
	@echo "  OS: $(DETECTED_OS)"
	@echo "  Scripts: .$(SHELL_EXT) files"

# Build targets
build:
	@echo "Building entire solution..."
	dotnet build Castellan.sln --configuration Release

build-worker:
	@echo "Building Worker service..."
	dotnet build $(SRC_DIR)$(PATH_SEP)Castellan.Worker$(PATH_SEP)Castellan.Worker.csproj --configuration Release

build-admin:
	@echo "Building React admin interface..."
	cd $(ADMIN_DIR) && npm run build

# Test targets
test:
	@echo "Running all tests..."
	$(SHELL_CMD) $(SCRIPT_TEST)

test-verbose:
	@echo "Running tests with detailed output..."
ifeq ($(DETECTED_OS),Windows)
	$(SHELL_CMD) $(SCRIPT_TEST) -Verbosity detailed
else
	$(SHELL_CMD) $(SCRIPT_TEST) --verbosity detailed
endif

test-no-build:
	@echo "Running tests without building..."
ifeq ($(DETECTED_OS),Windows)
	$(SHELL_CMD) $(SCRIPT_TEST) -NoBuild
else
	$(SHELL_CMD) $(SCRIPT_TEST) --no-build
endif

test-single:
	@echo "Run a single test by providing TEST_FILTER variable"
	@echo "Example: make test-single TEST_FILTER=TestMethodName"
ifdef TEST_FILTER
	dotnet test $(SRC_DIR)$(PATH_SEP)Castellan.Tests$(PATH_SEP)Castellan.Tests.csproj --filter "FullyQualifiedName~$(TEST_FILTER)"
else
	@echo "ERROR: TEST_FILTER variable is required"
	@exit 1
endif

test-coverage:
	@echo "Running tests with code coverage..."
	dotnet test $(SRC_DIR)$(PATH_SEP)Castellan.Tests$(PATH_SEP)Castellan.Tests.csproj --collect:"XPlat Code Coverage"

# Service management targets
start:
	@echo "Starting all services (background by default)..."
	$(SHELL_CMD) $(SCRIPT_START)

start-fg:
	@echo "Starting all services (foreground)..."
ifeq ($(DETECTED_OS),Windows)
	$(SHELL_CMD) $(SCRIPT_START) -Foreground
else
	$(SHELL_CMD) $(SCRIPT_START) --foreground
endif

start-quick:
	@echo "Starting services without building..."
ifeq ($(DETECTED_OS),Windows)
	$(SHELL_CMD) $(SCRIPT_START) -NoBuild
else
	$(SHELL_CMD) $(SCRIPT_START) --no-build
endif

stop:
	@echo "Stopping all services gracefully..."
	$(SHELL_CMD) $(SCRIPT_STOP)

stop-force:
	@echo "Force stopping all services..."
ifeq ($(DETECTED_OS),Windows)
	$(SHELL_CMD) $(SCRIPT_STOP) -Force
else
	$(SHELL_CMD) $(SCRIPT_STOP) --force
endif

stop-all:
	@echo "Stopping all services including Qdrant and Ollama..."
ifeq ($(DETECTED_OS),Windows)
	$(SHELL_CMD) $(SCRIPT_STOP) -StopQdrant -StopOllama
else
	$(SHELL_CMD) $(SCRIPT_STOP) --stop-qdrant --stop-ollama
endif

# Status and validation targets
status:
	@echo "Checking component status..."
	$(SHELL_CMD) $(SCRIPT_STATUS)

status-detail:
	@echo "Checking detailed component status..."
ifeq ($(DETECTED_OS),Windows)
	$(SHELL_CMD) $(SCRIPT_STATUS) -Detailed
else
	$(SHELL_CMD) $(SCRIPT_STATUS) --detailed
endif

validate:
	@echo "Validating scripts..."
	$(SHELL_CMD) $(SCRIPT_VALIDATE)

# Utility targets
clean:
	@echo "Cleaning build artifacts..."
	dotnet clean Castellan.sln
	cd $(ADMIN_DIR) && rm -rf build/ node_modules/.cache/ || true

install-deps:
	@echo "Installing development dependencies..."
	@echo "Installing .NET dependencies..."
	dotnet restore Castellan.sln
	@echo "Installing Node.js dependencies..."
	cd $(ADMIN_DIR) && npm install
ifeq ($(DETECTED_OS),Windows)
	@echo ""
	@echo "Optional Windows tools:"
	@echo "  PSScriptAnalyzer: Install-Module PSScriptAnalyzer -Scope CurrentUser"
else
	@echo ""
	@echo "Optional Unix tools:"
	@echo "  shellcheck: sudo apt-get install shellcheck (Ubuntu/Debian)"
	@echo "  shellcheck: brew install shellcheck (macOS)"
endif

setup-env:
	@echo "Setting up development environment..."
	@echo ""
	@echo "1. Copy appsettings template:"
ifeq ($(DETECTED_OS),Windows)
	@echo "   Copy-Item $(SRC_DIR)$(PATH_SEP)Castellan.Worker$(PATH_SEP)appsettings.template.json $(SRC_DIR)$(PATH_SEP)Castellan.Worker$(PATH_SEP)appsettings.json"
else
	@echo "   cp $(SRC_DIR)/Castellan.Worker/appsettings.template.json $(SRC_DIR)/Castellan.Worker/appsettings.json"
endif
	@echo ""
	@echo "2. Set required environment variables:"
	@echo "   AUTHENTICATION__JWT__SECRETKEY (64+ characters)"
	@echo "   AUTHENTICATION__ADMINUSER__USERNAME"
	@echo "   AUTHENTICATION__ADMINUSER__PASSWORD"
	@echo ""
	@echo "3. Optional: Configure AI providers"
	@echo "   EMBEDDINGS__PROVIDER=Ollama or OpenAI"
	@echo "   LLM__PROVIDER=Ollama or OpenAI" 
	@echo "   OPENAI_API_KEY (if using OpenAI)"
	@echo ""
	@echo "4. Install Ollama models (if using local AI):"
	@echo "   ollama pull nomic-embed-text"
	@echo "   ollama pull llama3.1:8b-instruct-q8_0"
	@echo ""
	@echo "Run 'make install-deps' to install all dependencies"

# Docker management helpers
docker-qdrant:
	@echo "Starting Qdrant container..."
	docker run -d --name qdrant -p 6333:6333 qdrant/qdrant || docker start qdrant

docker-clean:
	@echo "Cleaning up Docker containers..."
	-docker stop qdrant
	-docker rm qdrant

# Development shortcuts
dev: install-deps setup-env
	@echo "Development environment ready!"
	@echo "Next steps:"
	@echo "1. Configure appsettings.json and environment variables"
	@echo "2. Run 'make start' to start all services"

quick-start: start-quick

restart: stop start

logs:
	@echo "Showing recent logs..."
ifeq ($(DETECTED_OS),Windows)
	@echo "PowerShell logs location: %LOCALAPPDATA%\\Castellan\\logs"
	-Get-ChildItem "$env:LOCALAPPDATA\\Castellan\\logs" -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | Get-Content -Tail 20
else
	@echo "Bash logs location: /tmp/castellan-worker.log"
	-tail -20 /tmp/castellan-worker.log 2>/dev/null || echo "No logs found"
endif

# Make executable (Unix-like systems only)
ifneq ($(DETECTED_OS),Windows)
make-executable:
	@echo "Making bash scripts executable..."
	chmod +x $(SCRIPTS_DIR)/*.sh
endif
