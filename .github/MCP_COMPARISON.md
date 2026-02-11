# MCP Configuration: VS Code vs GitHub Copilot Workspace

## Quick Comparison

| Feature | VS Code | GitHub Copilot Workspace |
|---------|---------|--------------------------|
| **Config Location** | `.vscode/mcp.json` | `~/.config/copilot/mcp.json` (Linux/Mac)<br>`%APPDATA%\GitHub\Copilot\mcp.json` (Windows) |
| **Scope** | Workspace-local | User-level |
| **Auto-loads?** | ✅ Yes | ❌ No (manual setup) |
| **Same servers?** | ✅ Yes | ✅ Yes (copy from .vscode/) |
| **Same format?** | ✅ Yes | ✅ Yes (identical JSON) |
| **Works when?** | Opening workspace in VS Code | Running Copilot Workspace/CLI |
| **Setup required?** | ❌ No (included in repo) | ✅ Yes (user must configure) |

## The Key Difference

```
VS Code Extension              Copilot Workspace
      ↓                               ↓
Reads workspace .vscode/       Reads user home directory
      ↓                               ↓
    Works                          Needs setup
```

## What To Do

### If Using VS Code Only
✅ **Nothing needed** - The `.vscode/mcp.json` in this repository works automatically.

### If Using GitHub Copilot Workspace
⚠️ **Setup required** - Follow one of these guides:

1. **Quick setup** (copy-paste): [MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md)
2. **Full guide** (detailed): [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md)
3. **Architecture** (understand why): [MCP_ARCHITECTURE.md](MCP_ARCHITECTURE.md)

## One-Command Setup

### Linux/macOS
```bash
mkdir -p ~/.config/copilot && cp .vscode/mcp.json ~/.config/copilot/mcp.json
```

### Windows (PowerShell)
```powershell
New-Item -ItemType Directory -Force -Path "$env:APPDATA\GitHub\Copilot" | Out-Null
Copy-Item -Path ".vscode\mcp.json" -Destination "$env:APPDATA\GitHub\Copilot\mcp.json"
```

## Why Both Files Exist

1. **Repository `.vscode/mcp.json`**: For VS Code users (version-controlled)
2. **User config file**: For Copilot Workspace users (not in repo)

Both point to the same MCP servers with identical configuration.

## Troubleshooting One-Liners

```bash
# Check if config exists (Linux/Mac)
ls -la ~/.config/copilot/mcp.json

# Check if config exists (Windows)
Test-Path "$env:APPDATA\GitHub\Copilot\mcp.json"

# Validate JSON (Linux/Mac)
cat ~/.config/copilot/mcp.json | python3 -m json.tool

# Validate JSON (Windows)
Get-Content "$env:APPDATA\GitHub\Copilot\mcp.json" | ConvertFrom-Json

# Check prerequisites
node --version && npm --version && python3 --version && uvx --version
```

## MCP Servers in This Repository

| Server | Purpose | Command |
|--------|---------|---------|
| **sequential-thinking** | Advanced reasoning | `npx @modelcontextprotocol/server-sequential-thinking` |
| **context7** | Documentation lookup | `npx @upstash/context7-mcp` |
| **memory** | Context storage | `npx @modelcontextprotocol/server-memory` |
| **serena** | Code analysis | `uvx serena` (from git) |

## Support

If setup doesn't work:
1. Verify prerequisites are installed (Node.js, npm, Python, uvx)
2. Check JSON syntax in config file
3. Ensure file is in correct location
4. Restart Copilot Workspace after creating config
5. See [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md) troubleshooting section

## Related Files

- 📖 [Full Setup Guide](COPILOT_WORKSPACE_MCP_SETUP.md)
- ⚡ [Quick Setup](MCP_QUICK_SETUP.md)
- 🏗️ [Architecture](MCP_ARCHITECTURE.md)
- ✅ [Checklist](MCP_CONFIGURATION_CHECKLIST.md)
- 🏠 [Back to agents.md](../agents.md)
