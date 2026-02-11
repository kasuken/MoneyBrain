# MCP Server Configuration Checklist

This checklist helps ensure MCP servers are properly configured for GitHub Copilot Workspace.

## ✅ Pre-Setup Checklist

Before configuring MCP servers, ensure you have:

- [ ] Node.js installed (check: `node --version`)
- [ ] npm installed (check: `npm --version`)
- [ ] Python 3.8+ installed (check: `python3 --version`)
- [ ] uv package manager installed (check: `uvx --version`)

## 🔧 Configuration Checklist

### For VS Code (Workspace-Local)

- [ ] `.vscode/mcp.json` exists in the repository
- [ ] File contains all 4 MCP servers: sequential-thinking, context7, memory, serena
- [ ] JSON syntax is valid (test with: `cat .vscode/mcp.json | python3 -m json.tool`)

### For GitHub Copilot Workspace (User-Level)

- [ ] Created config directory:
  - Linux/macOS: `~/.config/copilot/`
  - Windows: `%APPDATA%\GitHub\Copilot\`

- [ ] Created `mcp.json` in config directory with same content as `.vscode/mcp.json`

- [ ] File permissions allow read access

- [ ] JSON syntax is valid

## 🧪 Testing Checklist

- [ ] VS Code loads MCP servers (check VS Code output/logs)
- [ ] Copilot Workspace can access MCP servers
- [ ] Test sequential-thinking: Ask Copilot to "use sequential thinking to analyze..."
- [ ] Test context7: Ask Copilot to "look up documentation for [library]"
- [ ] Test memory: Ask Copilot to "remember this pattern..."
- [ ] Test serena: Ask Copilot to perform code analysis

## 🔍 Troubleshooting Steps

If MCP servers don't load:

- [ ] Verified config file location is correct
- [ ] Checked JSON syntax with validator
- [ ] Verified prerequisites are installed
- [ ] Restarted Copilot Workspace/VS Code
- [ ] Checked for error messages in logs
- [ ] Tested MCP servers individually (run commands manually)

## 📚 Resources

- Setup Guide: [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md)
- Repository agents.md: [../agents.md](../agents.md)
- VS Code MCP config: [../.vscode/mcp.json](../.vscode/mcp.json)

## 💡 Common Issues

### Issue: "sequential-thinking not found"
- **Solution**: Ensure Node.js and npm are installed and in PATH

### Issue: "serena fails to start"
- **Solution**: Install uv package manager and verify Python 3.8+

### Issue: "Copilot can't find mcp.json"
- **Solution**: Check that config file is in user home directory, not workspace directory

### Issue: "${workspaceFolder} not resolved"
- **Solution**: This is expected for serena. The variable is resolved at runtime by Copilot Workspace.
