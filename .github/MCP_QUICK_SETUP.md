# Quick MCP Setup for GitHub Copilot Coding Agent

## ⚠️ Important: Repository Administrator Required

Configuring MCP servers for GitHub Copilot Coding Agent requires **repository administrator access** and must be done in the **GitHub repository settings**, not on your local machine.

## TL;DR - For Repository Administrators

1. Go to repository **Settings** on GitHub.com
2. Click **Copilot** → **Coding agent** 
3. Paste the JSON configuration below into **MCP configuration**
4. Click **Save**

## MCP Configuration JSON

Copy this entire block into the repository settings:

```json
{
  "mcpServers": {
    "sequential-thinking": {
      "type": "local",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"],
      "tools": ["*"]
    },
    "context7": {
      "type": "local",
      "command": "npx",
      "args": ["-y", "@upstash/context7-mcp"],
      "tools": ["*"]
    },
    "memory": {
      "type": "local",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-memory"],
      "tools": ["*"]
    },
    "serena": {
      "type": "local",
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
```

## Setup Dependencies (Optional)

If the `serena` server needs `uvx`, create `.github/workflows/copilot-setup-steps.yml`:

```yaml
on:
  workflow_dispatch:

permissions:
  id-token: write
  contents: read

jobs:
  copilot-setup-steps:
    runs-on: ubuntu-latest
    environment: copilot
    steps:
      - name: Install uv
        run: |
          curl -LsSf https://astral.sh/uv/install.sh | sh
          echo "$HOME/.cargo/bin" >> $GITHUB_PATH
```

## Prerequisites

MCP servers require:
- ✅ Node.js (for sequential-thinking, context7, memory) - Usually available on GitHub Actions
- ✅ uv/uvx (for serena) - Add via `copilot-setup-steps.yml` if needed

## Validate Your Setup

After configuring in repository settings:

1. **Create a test issue** in the repository
2. **Assign it to Copilot** 
3. **Wait for the pull request** to be created
4. **View the session logs**:
   - Open the PR
   - Click "View session"
   - Expand **Start MCP Servers** step
   - Verify all servers are listed

## Key Differences from VS Code

| Aspect | VS Code | Copilot Coding Agent |
|--------|---------|---------------------|
| **Config Location** | `.vscode/mcp.json` (in repo) | Repository Settings (on GitHub.com) |
| **JSON Format** | `"servers"` | `"mcpServers"` |
| **Type Value** | `"stdio"` | `"local"` |
| **Who Configures** | Anyone with repo access | Repository administrators only |
| **Scope** | Local development only | Entire repository |

## Common Mistakes

❌ **Don't** copy files to `~/.config/copilot/` (old/incorrect approach)  
❌ **Don't** use `"servers"` (VS Code format)  
❌ **Don't** use `"type": "stdio"` (use `"local"` instead)  
✅ **Do** configure in repository settings on GitHub.com  
✅ **Do** use `"mcpServers"` format  
✅ **Do** include `"tools"` array for each server  

## Full Documentation

See [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md) for:
- Detailed step-by-step instructions
- Troubleshooting
- Format conversion guide

## References

- [Official GitHub Documentation](https://docs.github.com/en/enterprise-cloud@latest/copilot/how-tos/use-copilot-agents/coding-agent/extend-coding-agent-with-mcp)
- [Model Context Protocol](https://modelcontextprotocol.io/)
