# MCP Server Documentation Index

This directory contains comprehensive documentation for configuring Model Context Protocol (MCP) servers with GitHub Copilot Workspace.

## 🎯 Start Here

**Are you new to MCP configuration?** Start with the **Quick Setup** guide:

➡️ **[MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md)** - Copy-paste commands to get started immediately

## 📚 Documentation Suite

### For End Users

1. **[MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md)** ⚡
   - Quick copy-paste setup commands
   - OS-specific (Linux/macOS/Windows)
   - Prerequisites checklist
   - **Best for**: Getting up and running fast

2. **[MCP_COMPARISON.md](MCP_COMPARISON.md)** 📊
   - VS Code vs Copilot Workspace side-by-side
   - One-command setup
   - Troubleshooting one-liners
   - **Best for**: Understanding the difference

3. **[COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md)** 📖
   - Detailed step-by-step instructions
   - Comprehensive troubleshooting
   - Alternative configuration methods
   - **Best for**: Deep dive and problem solving

### For Understanding

4. **[MCP_ARCHITECTURE.md](MCP_ARCHITECTURE.md)** 🏗️
   - Visual diagrams and flow charts
   - Architecture explanation
   - Execution context differences
   - **Best for**: Visual learners and architects

### For Maintainers

5. **[MCP_CONFIGURATION_CHECKLIST.md](MCP_CONFIGURATION_CHECKLIST.md)** ✅
   - Pre-setup checklist
   - Configuration verification steps
   - Testing procedures
   - Common issues and solutions
   - **Best for**: Repository maintainers

## 🚀 Quick Reference

### The Problem
`.vscode/mcp.json` works in VS Code but not in GitHub Copilot Workspace.

### The Solution
Copy the configuration to your user home directory:

**Linux/macOS:**
```bash
mkdir -p ~/.config/copilot && cp .vscode/mcp.json ~/.config/copilot/mcp.json
```

**Windows (PowerShell):**
```powershell
New-Item -ItemType Directory -Force -Path "$env:APPDATA\GitHub\Copilot" | Out-Null
Copy-Item -Path ".vscode\mcp.json" -Destination "$env:APPDATA\GitHub\Copilot\mcp.json"
```

### Why Two Locations?
- **VS Code**: Uses workspace-local `.vscode/mcp.json` ✅
- **Copilot Workspace**: Uses user-level config in home directory ⚠️

## 🔍 MCP Servers in This Repository

| Server | Purpose |
|--------|---------|
| **sequential-thinking** | Advanced reasoning and analysis |
| **context7** | Library documentation lookup |
| **memory** | Persistent context storage |
| **serena** | Code analysis and symbol operations |

## 📋 Workflow

```
1. Read Quick Setup guide
   ↓
2. Install prerequisites (Node.js, npm, Python, uvx)
   ↓
3. Copy configuration to user directory
   ↓
4. Restart Copilot Workspace
   ↓
5. Verify MCP servers load
   ↓
6. If issues → Check Troubleshooting section
```

## 🆘 Need Help?

1. **Quick answer**: See [MCP_COMPARISON.md](MCP_COMPARISON.md#troubleshooting-one-liners)
2. **Detailed help**: See [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md#troubleshooting)
3. **Verify setup**: Use [MCP_CONFIGURATION_CHECKLIST.md](MCP_CONFIGURATION_CHECKLIST.md)

## 🔗 Related Files

- **Repository root**: [../agents.md](../agents.md) - AI agent guidance
- **Main README**: [../README.md](../README.md) - Project overview
- **VS Code config**: [../.vscode/mcp.json](../.vscode/mcp.json) - Source of truth for MCP servers

## 💡 Key Takeaways

1. ✅ `.vscode/mcp.json` works automatically in VS Code
2. ⚠️ GitHub Copilot Workspace needs user-level configuration
3. 📋 Same servers, same format, different location
4. 🔄 Copy once, works everywhere (same user account)
5. 🚀 Setup takes < 1 minute with quick setup guide

---

**Documentation Version**: 1.0  
**Last Updated**: 2026-02-11  
**Maintained by**: MoneyBrain Contributors
