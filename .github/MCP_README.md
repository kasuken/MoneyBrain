# MCP Server Documentation Index

This directory contains documentation for configuring Model Context Protocol (MCP) servers with GitHub Copilot.

## 🎯 Start Here

**Repository Administrators**: Configure MCP servers in repository settings  
➡️ **[MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md)** - Quick configuration guide

**VS Code Users**: MCP configuration already included  
➡️ `.vscode/mcp.json` - Works automatically

## 📚 Documentation Suite

### For Repository Administrators

1. **[MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md)** ⚡
   - Repository settings configuration steps
   - JSON configuration to copy-paste
   - Prerequisites and validation
   - **Best for**: Quick setup (repository admins)

2. **[MCP_COMPARISON.md](MCP_COMPARISON.md)** 📊
   - VS Code vs Copilot Coding Agent side-by-side
   - Configuration format differences
   - Common mistakes to avoid
   - **Best for**: Understanding the differences

3. **[COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md)** 📖
   - Detailed step-by-step instructions
   - Comprehensive troubleshooting
   - Format conversion guide
   - **Best for**: In-depth understanding

### For Understanding

4. **[MCP_ARCHITECTURE.md](MCP_ARCHITECTURE.md)** 🏗️
   - Visual diagrams and flow charts
   - Architecture explanation
   - Configuration differences
   - **Best for**: Visual learners

5. **[MCP_CONFIGURATION_CHECKLIST.md](MCP_CONFIGURATION_CHECKLIST.md)** ✅
   - Configuration verification steps
   - Testing procedures
   - **Best for**: Verification

## 🚀 Quick Reference

### The Issue
`.vscode/mcp.json` works in VS Code but GitHub Copilot Coding Agent needs repository settings configuration.

### The Solution (Repository Admins Only)
1. Go to repository **Settings** on GitHub.com
2. Click **Copilot** → **Coding agent**
3. Paste MCP configuration JSON
4. Click **Save**

See [MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md) for the JSON configuration.

### Configuration Formats

**VS Code** (`.vscode/mcp.json`):
```json
{ "servers": { "name": { "type": "stdio", ... } } }
```

**Copilot Coding Agent** (Repository Settings):
```json
{ "mcpServers": { "name": { "type": "local", "tools": [...], ... } } }
```

## 🔍 MCP Servers in This Repository

| Server | Purpose |
|--------|---------|
| **sequential-thinking** | Advanced reasoning and analysis |
| **context7** | Library documentation lookup |
| **memory** | Persistent context storage |
| **serena** | Code analysis and symbol operations |

## 📋 Setup Workflow

```
Repository Administrator
    ↓
Access GitHub.com Settings
    ↓
Navigate to Copilot → Coding agent
    ↓
Paste MCP configuration JSON
    ↓
Save & validate
    ↓
Test with Copilot issue assignment
```

## 🆘 Need Help?

1. **Quick answer**: See [MCP_COMPARISON.md](MCP_COMPARISON.md#common-mistakes-to-avoid)
2. **Setup help**: See [MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md)
3. **Detailed help**: See [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md#troubleshooting)

## 🔗 Related Files

- **Repository root**: [../agents.md](../agents.md) - AI agent guidance
- **Main README**: [../README.md](../README.md) - Project overview
- **VS Code config**: [../.vscode/mcp.json](../.vscode/mcp.json) - VS Code MCP configuration

## 💡 Key Takeaways

1. ✅ `.vscode/mcp.json` works automatically in VS Code
2. ⚠️ GitHub Copilot Coding Agent requires repository settings configuration
3. 📋 Different formats: `"servers"` vs `"mcpServers"`, `"stdio"` vs `"local"`
4. 👤 Only repository administrators can configure Copilot Coding Agent
5. 🚀 Setup takes < 5 minutes with admin access

## ❌ Previous Incorrect Approach

Earlier versions of this documentation incorrectly suggested copying files to `~/.config/copilot/`. This is **not the correct approach**. The proper method is to configure MCP servers in the repository settings on GitHub.com.

---

**Documentation Version**: 2.0 (Corrected)  
**Last Updated**: 2026-02-11  
**Reference**: [GitHub Copilot MCP Documentation](https://docs.github.com/en/enterprise-cloud@latest/copilot/how-tos/use-copilot-agents/coding-agent/extend-coding-agent-with-mcp)
