#!/bin/bash
# MCP Configuration Validation Script
# Checks if MCP servers are properly configured for GitHub Copilot Workspace

set -e

echo "=================================="
echo "MCP Configuration Validator"
echo "=================================="
echo ""

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

ERRORS=0
WARNINGS=0

# Function to check command
check_command() {
    if command -v $1 &> /dev/null; then
        echo -e "${GREEN}✓${NC} $1 is installed"
        return 0
    else
        echo -e "${RED}✗${NC} $1 is NOT installed"
        ERRORS=$((ERRORS + 1))
        return 1
    fi
}

# Function to check file
check_file() {
    if [ -f "$1" ]; then
        echo -e "${GREEN}✓${NC} Found: $1"
        return 0
    else
        echo -e "${RED}✗${NC} Missing: $1"
        ERRORS=$((ERRORS + 1))
        return 1
    fi
}

# Function to validate JSON
validate_json() {
    if python3 -m json.tool "$1" > /dev/null 2>&1; then
        echo -e "${GREEN}✓${NC} Valid JSON: $1"
        return 0
    else
        echo -e "${RED}✗${NC} Invalid JSON: $1"
        ERRORS=$((ERRORS + 1))
        return 1
    fi
}

echo "1. Checking prerequisites..."
echo "------------------------------"
check_command node
check_command npm
check_command npx
check_command python3
check_command uvx

echo ""
echo "2. Checking VS Code configuration..."
echo "------------------------------"
if [ -f ".vscode/mcp.json" ]; then
    check_file ".vscode/mcp.json"
    validate_json ".vscode/mcp.json"
else
    echo -e "${YELLOW}⚠${NC} .vscode/mcp.json not found (run from repository root)"
    WARNINGS=$((WARNINGS + 1))
fi

echo ""
echo "3. Checking Copilot Workspace configuration..."
echo "------------------------------"

# Determine OS and check appropriate location
if [[ "$OSTYPE" == "darwin"* ]] || [[ "$OSTYPE" == "linux-gnu"* ]]; then
    CONFIG_PATH="$HOME/.config/copilot/mcp.json"
    echo "OS: Linux/macOS"
    echo "Expected config: $CONFIG_PATH"
    
    if [ -f "$CONFIG_PATH" ]; then
        check_file "$CONFIG_PATH"
        validate_json "$CONFIG_PATH"
        
        # Check if it matches the repository version
        if [ -f ".vscode/mcp.json" ]; then
            if diff -q "$CONFIG_PATH" ".vscode/mcp.json" > /dev/null; then
                echo -e "${GREEN}✓${NC} User config matches repository config"
            else
                echo -e "${YELLOW}⚠${NC} User config differs from repository config"
                WARNINGS=$((WARNINGS + 1))
            fi
        fi
    else
        echo -e "${RED}✗${NC} Missing: $CONFIG_PATH"
        echo ""
        echo "To fix, run:"
        echo "  mkdir -p ~/.config/copilot"
        echo "  cp .vscode/mcp.json ~/.config/copilot/mcp.json"
        ERRORS=$((ERRORS + 1))
    fi
elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]]; then
    echo "OS: Windows"
    echo "Please run the PowerShell validation script instead."
    exit 1
fi

echo ""
echo "4. Checking MCP server definitions..."
echo "------------------------------"

if [ -f "$CONFIG_PATH" ]; then
    # Check for each server
    SERVERS=("sequential-thinking" "context7" "memory" "serena")
    for server in "${SERVERS[@]}"; do
        if grep -q "\"$server\"" "$CONFIG_PATH"; then
            echo -e "${GREEN}✓${NC} Server defined: $server"
        else
            echo -e "${RED}✗${NC} Server missing: $server"
            ERRORS=$((ERRORS + 1))
        fi
    done
fi

echo ""
echo "=================================="
echo "Validation Summary"
echo "=================================="
echo -e "Errors: ${RED}$ERRORS${NC}"
echo -e "Warnings: ${YELLOW}$WARNINGS${NC}"
echo ""

if [ $ERRORS -eq 0 ] && [ $WARNINGS -eq 0 ]; then
    echo -e "${GREEN}✓ Configuration is complete and valid!${NC}"
    echo ""
    echo "Next steps:"
    echo "1. Restart GitHub Copilot Workspace"
    echo "2. Test MCP servers by asking Copilot to use them"
    exit 0
elif [ $ERRORS -eq 0 ]; then
    echo -e "${YELLOW}⚠ Configuration is valid but has warnings${NC}"
    echo ""
    echo "Review warnings above and address them if needed."
    exit 0
else
    echo -e "${RED}✗ Configuration has errors that need to be fixed${NC}"
    echo ""
    echo "See .github/COPILOT_WORKSPACE_MCP_SETUP.md for detailed setup instructions."
    exit 1
fi
