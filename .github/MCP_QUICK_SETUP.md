# Quick MCP Setup for GitHub Copilot Workspace

## TL;DR - Copy and Paste

### Linux/macOS

```bash
# Create directory
mkdir -p ~/.config/copilot

# Create config (copy entire block)
cat > ~/.config/copilot/mcp.json << 'EOF'
{
  "servers": {
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"],
      "tools": ["*"]
    },
    "context7": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@upstash/context7-mcp"],
      "tools": ["*"]
    },
    "memory": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-memory"],
      "tools": ["*"]
    },
    "serena": {
      "type": "stdio",
      "command": "uvx",
      "args": [
        "--from",
        "git+https://github.com/oraios/serena",
        "serena",
        "start-mcp-server",
        "--context",
        "ide-assistant",
        "--enable-web-dashboard",
        "False",
        "--project",
        "${workspaceFolder}"
      ],
      "tools": ["*"]
    }
  }
}
EOF

# Verify
cat ~/.config/copilot/mcp.json
```

### Windows (PowerShell)

```powershell
# Create directory
New-Item -ItemType Directory -Force -Path "$env:APPDATA\GitHub\Copilot"

# Create config (copy entire block)
@"
{
  "servers": {
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"],
      "tools": ["*"]
    },
    "context7": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@upstash/context7-mcp"],
      "tools": ["*"]
    },
    "memory": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-memory"],
      "tools": ["*"]
    },
    "serena": {
      "type": "stdio",
      "command": "uvx",
      "args": [
        "--from",
        "git+https://github.com/oraios/serena",
        "serena",
        "start-mcp-server",
        "--context",
        "ide-assistant",
        "--enable-web-dashboard",
        "False",
        "--project",
        "${workspaceFolder}"
      ],
      "tools": ["*"]
    }
  }
}
"@ | Out-File -FilePath "$env:APPDATA\GitHub\Copilot\mcp.json" -Encoding UTF8

# Verify
Get-Content "$env:APPDATA\GitHub\Copilot\mcp.json"
```

## Prerequisites

Install before configuring MCP:

```bash
# Check if installed
node --version   # Need: v16+
npm --version    # Need: 8+
python3 --version # Need: 3.8+
uvx --version    # Need: uv package manager

# Install if missing:
# - Node.js: https://nodejs.org/
# - uv: https://docs.astral.sh/uv/
```

## Restart Required

After creating the config:
1. Close and restart GitHub Copilot Workspace
2. Or restart VS Code if using Copilot there

## Validate Your Setup

Run the validation script to check everything is configured correctly:

**Linux/macOS:**
```bash
cd /path/to/MoneyBrain
./.github/validate-mcp-config.sh
```

**Windows (PowerShell):**
```powershell
cd C:\path\to\MoneyBrain
.\.github\validate-mcp-config.ps1
```

## Full Documentation

See [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md) for:
- Troubleshooting
- Detailed explanations
- Alternative configuration methods
