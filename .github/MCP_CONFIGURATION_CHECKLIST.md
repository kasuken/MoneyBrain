# MCP Server Configuration Checklist

This checklist helps repository administrators configure MCP servers for GitHub Copilot Coding Agent.

## ✅ Pre-Configuration Checklist

Before configuring MCP servers, verify:

- [ ] You have **repository administrator** access
- [ ] Repository has GitHub Copilot enabled
- [ ] You can access repository **Settings** on GitHub.com

## 🔧 Configuration Checklist

### Access Repository Settings

- [ ] Navigate to repository on GitHub.com
- [ ] Click **Settings** tab
- [ ] In sidebar, under "Code & automation", click **Copilot**
- [ ] Click **Coding agent**

### Add MCP Configuration

- [ ] Copy the MCP configuration JSON (see [MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md))
- [ ] Paste into **MCP configuration** field
- [ ] Verify JSON format:
  - [ ] Uses `"mcpServers"` (not `"servers"`)
  - [ ] Each server has `"type": "local"`
  - [ ] Each server has `"tools"` array
  - [ ] All 4 servers included: sequential-thinking, context7, memory, serena
- [ ] Click **Save**
- [ ] Verify GitHub validates the JSON (no errors shown)

### Optional: Setup Dependencies

If serena MCP server needs uvx:

- [ ] Create `.github/workflows/copilot-setup-steps.yml`
- [ ] Add uv installation step
- [ ] Commit and push workflow file

## 🧪 Testing Checklist

After configuration in repository settings:

- [ ] Create a test issue in the repository
- [ ] Assign the issue to Copilot
- [ ] Wait for Copilot to create a pull request
- [ ] Open the PR
- [ ] Click "View session" when available
- [ ] In log viewer, click ellipsis (...) → **Copilot**
- [ ] Expand **Start MCP Servers** step
- [ ] Verify all 4 servers are listed:
  - [ ] sequential-thinking
  - [ ] context7
  - [ ] memory
  - [ ] serena

## 🔍 Verification Checklist

### VS Code Configuration (Reference Only)

- [ ] `.vscode/mcp.json` exists in repository
- [ ] File uses `"servers"` format (correct for VS Code)
- [ ] File uses `"type": "stdio"` (correct for VS Code)
- [ ] JSON syntax is valid

Note: VS Code config is for reference only. Copilot Coding Agent uses repository settings.

### Repository Settings Configuration

- [ ] Configuration saved in Settings → Copilot → Coding agent
- [ ] Uses `"mcpServers"` format
- [ ] Uses `"type": "local"` for each server
- [ ] All servers have `"tools"` arrays
- [ ] Configuration validated by GitHub (no errors)

## 📋 Common Issues Checklist

If MCP servers don't load, check:

- [ ] You have repository administrator access
- [ ] Configuration is in **repository settings** (not local files)
- [ ] JSON format uses `"mcpServers"` (not `"servers"`)
- [ ] Type is `"local"` (not `"stdio"`)
- [ ] `"tools"` array is present for each server
- [ ] No JSON syntax errors
- [ ] Dependencies are available (uvx for serena)

## 💡 Common Mistakes to Avoid

- [ ] ❌ Don't copy files to `~/.config/copilot/`
- [ ] ❌ Don't use `"servers"` format in repository settings
- [ ] ❌ Don't use `"type": "stdio"` in repository settings
- [ ] ❌ Don't forget `"tools"` array
- [ ] ✅ Do configure in repository settings
- [ ] ✅ Do use `"mcpServers"` format
- [ ] ✅ Do use `"type": "local"`

## 📚 Resources

- Setup Guide: [MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md)
- Detailed Guide: [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md)
- Comparison: [MCP_COMPARISON.md](MCP_COMPARISON.md)
- Repository config: [../.vscode/mcp.json](../.vscode/mcp.json) (VS Code reference)
- Official docs: [GitHub Copilot MCP Documentation](https://docs.github.com/en/enterprise-cloud@latest/copilot/how-tos/use-copilot-agents/coding-agent/extend-coding-agent-with-mcp)

---

**Checklist Version**: 2.0 (Corrected)  
**Last Updated**: 2026-02-11
