# GitHub Copilot Workspace MCP Server Setup

> 📚 **Documentation Suite**
> - 🚀 **Quick Setup** - [MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md) - Copy-paste commands
> - 📊 **Comparison** - [MCP_COMPARISON.md](MCP_COMPARISON.md) - VS Code vs Copilot Workspace
> - 🏗️ **Architecture** - [MCP_ARCHITECTURE.md](MCP_ARCHITECTURE.md) - Visual diagrams
> - ✅ **Checklist** - [MCP_CONFIGURATION_CHECKLIST.md](MCP_CONFIGURATION_CHECKLIST.md) - Verification steps
> - 📖 **You are here** - Full setup guide with troubleshooting

## Overview

This repository uses Model Context Protocol (MCP) servers to enhance GitHub Copilot's capabilities. While the MCP configuration in `.vscode/mcp.json` works perfectly in VS Code, **GitHub Copilot Workspace requires a different configuration approach**.

## The Difference

| Environment | Configuration Location | Notes |
|-------------|----------------------|-------|
| **VS Code** | `.vscode/mcp.json` | ✅ Works automatically (workspace-local) |
| **GitHub Copilot Workspace** | User-level config | ⚠️ Requires manual setup |

## Required MCP Servers

This repository uses the following MCP servers:

1. **sequential-thinking** - Advanced reasoning and analysis
2. **context7** - Library documentation lookup
3. **memory** - Persistent context storage
4. **serena** - Code analysis and symbol operations

## Setup Instructions

### For Linux/macOS Users

1. **Create the Copilot config directory:**
   ```bash
   mkdir -p ~/.config/copilot
   ```

2. **Create the MCP configuration file:**
   ```bash
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
   ```

3. **Verify the file was created:**
   ```bash
   cat ~/.config/copilot/mcp.json
   ```

### For Windows Users

1. **Open PowerShell and create the directory:**
   ```powershell
   New-Item -ItemType Directory -Force -Path "$env:APPDATA\GitHub\Copilot"
   ```

2. **Create the MCP configuration file:**
   ```powershell
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
   ```

3. **Verify the file was created:**
   ```powershell
   Get-Content "$env:APPDATA\GitHub\Copilot\mcp.json"
   ```

## Prerequisites

Before the MCP servers will work, ensure you have the required tools installed:

### Node.js (for npx commands)
- **sequential-thinking**, **context7**, and **memory** require Node.js
- Install from: https://nodejs.org/
- Verify: `node --version` and `npm --version`

### Python/uv (for serena)
- **serena** requires Python and uvx (from uv package manager)
- Install uv from: https://docs.astral.sh/uv/
- Verify: `uvx --version`

## Testing Your Configuration

After setting up the MCP configuration:

1. **Restart GitHub Copilot Workspace/CLI** if it's already running

2. **Verify MCP servers load** by checking the logs (if available)

3. **Test in a Copilot session** by asking questions that would require MCP capabilities:
   - "Use sequential thinking to analyze this problem"
   - "Look up documentation for [library name]" (uses context7)
   - "Remember this pattern for future reference" (uses memory)

## Troubleshooting

### MCP Servers Not Loading

1. **Check file location:**
   - Linux/macOS: `~/.config/copilot/mcp.json`
   - Windows: `%APPDATA%\GitHub\Copilot\mcp.json`

2. **Verify JSON syntax:**
   ```bash
   # Linux/macOS
   cat ~/.config/copilot/mcp.json | python3 -m json.tool
   
   # Windows (PowerShell)
   Get-Content "$env:APPDATA\GitHub\Copilot\mcp.json" | ConvertFrom-Json
   ```

3. **Check prerequisites:**
   - `npx --version` - Should show npm version
   - `uvx --version` - Should show uv version

4. **Check permissions:**
   - Ensure the config file is readable by your user account

### Serena-Specific Issues

If serena fails to load:
- Ensure Python 3.8+ is installed: `python3 --version`
- Verify uv is installed: `uvx --version`
- Test serena directly: `uvx --from git+https://github.com/oraios/serena serena --help`

## Why Two Configuration Files?

You might notice we have MCP configuration in two places:

- **`.vscode/mcp.json`** - For VS Code extension (works locally in VS Code)
- **`~/.config/copilot/mcp.json`** - For GitHub Copilot Workspace/CLI (user-level)

This is necessary because:
1. VS Code reads workspace-local configs from `.vscode/`
2. GitHub Copilot Workspace runs in a different context and needs user-level config
3. Both environments use the same MCP server definitions

## Alternative: Project-Specific Environment

If you prefer not to use user-level configuration, you can set an environment variable:

```bash
export COPILOT_MCP_CONFIG=/path/to/your/mcp.json
```

Then point it to a project-specific config file. Note: This may require additional setup depending on how you launch Copilot Workspace.

## Additional Resources

- [Model Context Protocol Documentation](https://modelcontextprotocol.io/)
- [GitHub Copilot Documentation](https://docs.github.com/copilot)
- [Serena MCP Server](https://github.com/oraios/serena)

## Support

If you continue to experience issues:
1. Check that you're using the latest version of GitHub Copilot
2. Verify all prerequisites are installed
3. Review the troubleshooting steps above
4. Open an issue in this repository with:
   - Your operating system
   - Output of `node --version`, `npm --version`, `uvx --version`
   - Any error messages you're seeing
