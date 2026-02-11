# Quick MCP Setup for GitHub Copilot Coding Agent

## ⚠️ Important: Repository Administrator Required

Configuring MCP servers for GitHub Copilot Coding Agent requires **repository administrator access** and must be done in the **GitHub repository settings**.

## TL;DR - For Repository Administrators

**Good news**: You can reuse the existing `.vscode/mcp.json` configuration!

1. Go to repository **Settings** on GitHub.com
2. Click **Copilot** → **Coding agent** 
3. Copy the content from `.vscode/mcp.json` and paste it into **MCP configuration**
4. Click **Save**

That's it! The VS Code configuration already has the required `"tools"` key and can be used as-is.

## MCP Configuration JSON

The repository already has a complete MCP configuration in `.vscode/mcp.json`. 

Simply copy this entire file content into the repository settings:

```json
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
```

## Why This Works

According to the [official GitHub documentation](https://docs.github.com/en/enterprise-cloud@latest/copilot/how-tos/use-copilot-agents/coding-agent/extend-coding-agent-with-mcp#reusing-your-mcp-configuration-from-visual-studio-code), you can reuse VS Code MCP configuration directly as long as:

✅ Each server has a `"tools"` key (already present in `.vscode/mcp.json`)  
✅ No `inputs` or `envFile` are used (not applicable here)

The configuration works with both:
- `"servers"` (VS Code format) - **This is what we have**
- `"mcpServers"` (alternative format)

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

## Key Points

| Aspect | Details |
|--------|---------|
| **Config Source** | Copy from `.vscode/mcp.json` |
| **Config Destination** | Repository Settings → Copilot → Coding agent |
| **Format** | Use as-is (already has `"tools"` key) |
| **Who Configures** | Repository administrators only |
| **Scope** | Entire repository |

## Full Documentation

See [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md) for:
- Detailed step-by-step instructions
- Troubleshooting
- Alternative configuration options

## References

- [Official GitHub Documentation - Reusing VS Code MCP Configuration](https://docs.github.com/en/enterprise-cloud@latest/copilot/how-tos/use-copilot-agents/coding-agent/extend-coding-agent-with-mcp#reusing-your-mcp-configuration-from-visual-studio-code)
- [Model Context Protocol](https://modelcontextprotocol.io/)
