# MCP Configuration: VS Code vs GitHub Copilot Coding Agent

## Quick Comparison

| Feature | VS Code | GitHub Copilot Coding Agent |
|---------|---------|----------------------------|
| **Config Location** | `.vscode/mcp.json` (in repository) | Repository Settings on GitHub.com |
| **JSON Format** | `"servers": { ... }` | `"mcpServers": { ... }` |
| **Type Field** | `"type": "stdio"` | `"type": "local"` |
| **Scope** | Workspace-local (per developer) | Repository-wide (all users) |
| **Auto-loads?** | ✅ Yes (when opening workspace) | ✅ Yes (when configured in settings) |
| **Who Configures?** | Any developer with repo access | Repository administrators only |
| **Configuration Method** | Edit `.vscode/mcp.json` file | Use GitHub.com UI (Settings → Copilot) |

## The Key Difference

```
VS Code Extension                 Copilot Coding Agent
      ↓                                   ↓
Reads .vscode/mcp.json           Reads Repository Settings
(file in repository)              (configured on GitHub.com)
      ↓                                   ↓
  Local dev only                    Entire repository
```

## What To Do

### If Using VS Code Only
✅ **Configuration included** - The `.vscode/mcp.json` in this repository works automatically.

### If Using GitHub Copilot Coding Agent
⚠️ **Repository admin setup required**:

1. Go to repository **Settings** on GitHub.com (admin access required)
2. Navigate to **Copilot** → **Coding agent**
3. Add MCP configuration (see [MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md))

## Configuration Format Differences

### VS Code Format (`.vscode/mcp.json`)

```json
{
  "servers": {
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"],
      "tools": ["*"]
    }
  }
}
```

### Copilot Coding Agent Format (Repository Settings)

```json
{
  "mcpServers": {
    "sequential-thinking": {
      "type": "local",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"],
      "tools": ["*"]
    }
  }
}
```

**Key changes**:
- `"servers"` → `"mcpServers"`
- `"type": "stdio"` → `"type": "local"`
- `"tools"` array is required

## Quick Reference Commands

### Check Repository Settings (Admin Only)
```bash
# No command-line equivalent
# Must use GitHub.com UI: Settings → Copilot → Coding agent
```

### Validate VS Code Config
```bash
# Check if file exists
ls -la .vscode/mcp.json

# Validate JSON syntax
cat .vscode/mcp.json | python3 -m json.tool
```

### Test Copilot Coding Agent
1. Create an issue in the repository
2. Assign to Copilot
3. View session logs when PR is created
4. Check "Start MCP Servers" step

## MCP Servers in This Repository

| Server | Purpose |
|--------|---------|
| **sequential-thinking** | Advanced reasoning and analysis |
| **context7** | Library documentation lookup |
| **memory** | Persistent context storage |
| **serena** | Code analysis and symbol operations |

## Common Mistakes to Avoid

❌ **Copying files to `~/.config/copilot/`** - This was incorrect advice (now corrected)  
❌ **Using VS Code format in repository settings** - Won't work, needs conversion  
❌ **Forgetting to add `"tools"` array** - Required for Copilot Coding Agent  
✅ **Configuring in repository settings** - Correct approach for Copilot Coding Agent  
✅ **Using `"mcpServers"` format** - Required for Copilot Coding Agent  

## Why Both Configurations Exist

1. **`.vscode/mcp.json`** - For VS Code developers working locally
2. **Repository Settings** - For GitHub Copilot Coding Agent working on issues/PRs

They serve different purposes and use different formats, but reference the same MCP servers.

## Support

**For VS Code issues**:
- File is committed in `.vscode/mcp.json`
- Should work automatically when opening workspace

**For Copilot Coding Agent issues**:
1. Verify you're a repository administrator
2. Check configuration in Settings → Copilot → Coding agent
3. Review session logs for error messages
4. See [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md) troubleshooting

## Related Files

- 📖 [Full Setup Guide](COPILOT_WORKSPACE_MCP_SETUP.md)
- ⚡ [Quick Setup](MCP_QUICK_SETUP.md)
- 🏗️ [Architecture](MCP_ARCHITECTURE.md)
- ✅ [Checklist](MCP_CONFIGURATION_CHECKLIST.md)
- 🏠 [Back to README](MCP_README.md)
