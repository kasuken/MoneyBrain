# MCP Configuration: VS Code vs GitHub Copilot Coding Agent

## Quick Comparison

| Feature | VS Code | GitHub Copilot Coding Agent |
|---------|---------|----------------------------|
| **Config Location** | `.vscode/mcp.json` (in repository) | Repository Settings on GitHub.com |
| **Config Format** | Same JSON structure | Can reuse VS Code config directly! |
| **Scope** | Workspace-local (per developer) | Repository-wide (all users) |
| **Auto-loads?** | ✅ Yes (when opening workspace) | ✅ Yes (when configured in settings) |
| **Who Configures?** | Any developer with repo access | Repository administrators only |
| **Configuration Method** | Edit `.vscode/mcp.json` file | Copy `.vscode/mcp.json` to GitHub.com Settings |

## The Key Difference

```
VS Code Extension                 Copilot Coding Agent
      ↓                                   ↓
Reads .vscode/mcp.json           Reads Repository Settings
(file in repository)              (configured on GitHub.com)
      ↓                                   ↓
  Local dev only                    Entire repository
```

**Good News**: You can copy the same configuration between them!

## What To Do

### If Using VS Code Only
✅ **Configuration included** - The `.vscode/mcp.json` in this repository works automatically.

### If Using GitHub Copilot Coding Agent
⚠️ **Repository admin setup required** (but simple!):

1. Go to repository **Settings** on GitHub.com (admin access required)
2. Navigate to **Copilot** → **Coding agent**
3. Copy the content from `.vscode/mcp.json` and paste it into **MCP configuration**
4. Click **Save**

That's it! No conversion needed.

## Configuration Reuse

According to the [official GitHub documentation](https://docs.github.com/en/enterprise-cloud@latest/copilot/how-tos/use-copilot-agents/coding-agent/extend-coding-agent-with-mcp#reusing-your-mcp-configuration-from-visual-studio-code), you can reuse VS Code MCP configuration directly.

### Our Configuration (Already Ready!)

The `.vscode/mcp.json` file in this repository already has everything needed:

```json
{
  "servers": {
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"],
      "tools": ["*"]  ← Already has "tools" key!
    },
    ...
  }
}
```

✅ Has `"tools"` key for each server  
✅ No `inputs` or `envFile` to convert  
✅ Ready to use as-is

### What If It Needed Conversion?

The documentation mentions you'd need to:
1. Add `"tools"` key (already done ✅)
2. Convert `inputs` to `env` (not applicable here)
3. Convert `envFile` to `env` (not applicable here)

But our config already meets all requirements!

## Quick Reference

### Copy VS Code Config to Repository Settings
```bash
# 1. View the file
cat .vscode/mcp.json

# 2. Copy the JSON output
# 3. Paste into GitHub Settings → Copilot → Coding agent
```

## MCP Servers in This Repository

| Server | Purpose |
|--------|---------|
| **sequential-thinking** | Advanced reasoning and analysis |
| **context7** | Library documentation lookup |
| **memory** | Persistent context storage |
| **serena** | Code analysis and symbol operations |

## Common Questions

**Q: Do I need to change the format?**  
A: No! The `.vscode/mcp.json` already works for Copilot Coding Agent.

**Q: What about `"servers"` vs `"mcpServers"`?**  
A: Both work! The documentation accepts either format.

**Q: What about `"type": "stdio"` vs `"type": "local"`?**  
A: Both work! Keep it as `"stdio"`.

**Q: Do I need to add `"tools"`?**  
A: Already done! Each server in `.vscode/mcp.json` has `"tools": ["*"]`.

## Why Both Configurations Exist

1. **`.vscode/mcp.json`** - For VS Code developers working locally
2. **Repository Settings** - For GitHub Copilot Coding Agent working on issues/PRs

They use the same configuration, just stored in different locations.

## Support

**For VS Code issues**:
- File is committed in `.vscode/mcp.json`
- Should work automatically when opening workspace

**For Copilot Coding Agent issues**:
1. Verify you're a repository administrator
2. Check configuration in Settings → Copilot → Coding agent
3. Verify you copied the complete JSON from `.vscode/mcp.json`
4. Review session logs for error messages

## Related Files

- 📖 [Full Setup Guide](COPILOT_WORKSPACE_MCP_SETUP.md)
- ⚡ [Quick Setup](MCP_QUICK_SETUP.md)
- 🏗️ [Architecture](MCP_ARCHITECTURE.md)
- ✅ [Checklist](MCP_CONFIGURATION_CHECKLIST.md)
- 🏠 [Back to README](MCP_README.md)
