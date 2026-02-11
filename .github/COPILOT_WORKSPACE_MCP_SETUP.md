# GitHub Copilot Coding Agent MCP Server Setup

> 📚 **Documentation Suite**
> - 🚀 **Quick Setup** - [MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md) - Repository settings configuration
> - 📊 **Comparison** - [MCP_COMPARISON.md](MCP_COMPARISON.md) - VS Code vs Copilot Coding Agent
> - 🏗️ **Architecture** - [MCP_ARCHITECTURE.md](MCP_ARCHITECTURE.md) - Visual diagrams
> - ✅ **Checklist** - [MCP_CONFIGURATION_CHECKLIST.md](MCP_CONFIGURATION_CHECKLIST.md) - Verification steps
> - 📖 **You are here** - Full setup guide with troubleshooting

## Overview

This repository uses Model Context Protocol (MCP) servers to enhance GitHub Copilot's capabilities. The MCP configuration in `.vscode/mcp.json` works for VS Code, but **GitHub Copilot Coding Agent requires configuration in the repository settings on GitHub.com**.

> ⚠️ **Important**: Do NOT copy MCP configuration to your local machine. Configure it in GitHub repository settings instead.

## The Difference

| Environment | Configuration Location | Notes |
|-------------|----------------------|-------|
| **VS Code** | `.vscode/mcp.json` | ✅ Works automatically (workspace-local) |
| **GitHub Copilot Coding Agent** | Repository Settings on GitHub.com | ⚠️ Requires repository admin access |

## Configuration Approach

GitHub Copilot Coding Agent uses a **different configuration method** than VS Code:

1. **VS Code**: Reads `.vscode/mcp.json` file in the repository
2. **Copilot Coding Agent**: Configured via GitHub.com repository settings UI

The configuration must be added by a **repository administrator** in the repository settings.

## Required MCP Servers

This repository uses the following MCP servers:

1. **sequential-thinking** - Advanced reasoning and analysis
2. **context7** - Library documentation lookup
3. **memory** - Persistent context storage
4. **serena** - Code analysis and symbol operations

## Setup Instructions (Repository Administrators Only)

### Step 1: Access Repository Settings

1. Navigate to this repository on GitHub.com
2. Click **Settings** (you must be a repository administrator)
3. In the left sidebar, under "Code & automation", click **Copilot**
4. Click **Coding agent**

### Step 2: Add MCP Configuration

In the **MCP configuration** section, paste the following JSON:

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

### Step 3: Save Configuration

Click **Save** to save the configuration. GitHub will validate the JSON syntax automatically.

### Step 4: Setup Dependencies (if needed)

The `serena` MCP server requires `uvx` (from the `uv` package manager). If this dependency is not available on GitHub Actions runners by default, you may need to create a `.github/workflows/copilot-setup-steps.yml` file:

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

This ensures `uvx` is available when Copilot Coding Agent runs.

## Prerequisites

Before the MCP servers will work, ensure you have the required tools available:

### Node.js (for npx commands)
- **sequential-thinking**, **context7**, and **memory** require Node.js
- These are typically available on GitHub Actions runners by default
- For local development: Install from https://nodejs.org/

### Python/uv (for serena)
- **serena** requires Python and uvx (from uv package manager)
- May require setup via `copilot-setup-steps.yml` workflow (see above)
- For local development: Install uv from https://docs.astral.sh/uv/

## Testing Your Configuration

After setting up the MCP configuration in repository settings:

1. **Run the validation script** (for repository maintainers):
   
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
   
   Note: These scripts verify the VS Code configuration but don't validate the GitHub repository settings.

2. **Test with Copilot Coding Agent**:
   - Create an issue in the repository
   - Assign it to Copilot
   - Wait for Copilot to create a pull request
   - Open the PR and click "View session" when available
   - In the log viewer, click the ellipsis (**...**) → **Copilot**
   - Expand the **Start MCP Servers** step
   - Verify your MCP servers and their tools are listed

3. **Verify MCP capabilities** by asking Copilot questions that use the servers:
   - "Use sequential thinking to analyze this problem"
   - "Look up documentation for [library name]" (uses context7)
   - "Remember this pattern for future reference" (uses memory)
   - "Remember this pattern for future reference" (uses memory)

## Troubleshooting

### MCP Servers Not Loading

1. **Verify you're a repository administrator**: Only repository admins can configure MCP servers in settings

2. **Check the configuration in repository settings**:
   - Go to repository **Settings** → **Copilot** → **Coding agent**
   - Verify the JSON syntax is correct (GitHub validates on save)
   - Ensure all required fields are present (`type`, `command`, `args`, `tools`)

3. **Verify JSON format**:
   - Use `mcpServers` (not `servers`)
   - Include `type: "local"` for each server
   - Ensure `tools` array is present

4. **Check MCP server logs**:
   - Create a test issue and assign to Copilot
   - View the coding agent session logs
   - Check the "Start MCP Servers" step for errors

5. **Verify dependencies**:
   - Check if `uvx` is available for the serena server
   - Add `copilot-setup-steps.yml` workflow if needed

### Configuration Format Differences

The GitHub repository settings use a different JSON format than VS Code:

| VS Code `.vscode/mcp.json` | GitHub Repository Settings |
|----------------------------|----------------------------|
| `"servers": { ... }` | `"mcpServers": { ... }` |
| `"type": "stdio"` | `"type": "local"` |
| Optional `"tools"` | Required `"tools"` |

### Serena-Specific Issues

If serena fails to load:
- Ensure `uvx` is available via `copilot-setup-steps.yml`
- Check that the GitHub Actions runner has network access to clone from GitHub
- Verify the `${workspaceFolder}` variable is supported in the Copilot environment

## Why Two Configuration Files?

You might notice we have MCP configuration in two places:

- **`.vscode/mcp.json`** - For VS Code extension (workspace-local, automatic)
- **Repository Settings** - For GitHub Copilot Coding Agent (configured by repository admin)

This is necessary because:
1. VS Code reads workspace-local configs from `.vscode/`
2. GitHub Copilot Coding Agent uses repository settings on GitHub.com
3. Both environments use similar MCP server definitions but different formats

## Converting VS Code Config to Copilot Coding Agent

To adapt `.vscode/mcp.json` for Copilot Coding Agent:

1. Change `"servers"` to `"mcpServers"`
2. Change `"type": "stdio"` to `"type": "local"`
3. Ensure `"tools"` array is present for each server
4. If using `inputs`, switch to using `env` directly
5. If using `envFile`, switch to using `env` directly

## Additional Resources

- [GitHub Copilot Coding Agent MCP Documentation](https://docs.github.com/en/enterprise-cloud@latest/copilot/how-tos/use-copilot-agents/coding-agent/extend-coding-agent-with-mcp)
- [Model Context Protocol Documentation](https://modelcontextprotocol.io/)
- [VS Code MCP Servers Documentation](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [Serena MCP Server](https://github.com/oraios/serena)

## Support

If you continue to experience issues:
1. Verify you have repository administrator access
2. Check that MCP configuration is saved in repository settings
3. Review the Copilot Coding Agent session logs for error messages
4. Verify all prerequisites are available (Node.js, uvx)
5. Check the troubleshooting steps above
6. Open an issue in this repository with:
   - Your role (repository admin or contributor)
   - The Copilot Coding Agent session logs
   - Any error messages you're seeing
