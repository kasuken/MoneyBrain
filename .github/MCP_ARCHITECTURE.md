# MCP Architecture: VS Code vs Copilot Workspace

## Configuration Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                         MoneyBrain Repository                        │
│                                                                       │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │ .vscode/mcp.json                                               │ │
│  │ ┌────────────────────────────────────────────────────────────┐ │ │
│  │ │ {                                                          │ │ │
│  │ │   "servers": {                                             │ │ │
│  │ │     "sequential-thinking": { ... },                        │ │ │
│  │ │     "context7": { ... },                                   │ │ │
│  │ │     "memory": { ... },                                     │ │ │
│  │ │     "serena": { ... }                                      │ │ │
│  │ │   }                                                        │ │ │
│  │ │ }                                                          │ │ │
│  │ └────────────────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────┘ │
│                                  │                                   │
│                                  │                                   │
└──────────────────────────────────┼───────────────────────────────────┘
                                   │
                ┌──────────────────┴──────────────────┐
                │                                     │
                ▼                                     ▼
    ┌───────────────────────┐            ┌───────────────────────┐
    │      VS Code          │            │  Copilot Workspace    │
    │  (Local Development)  │            │    (CI/Cloud/CLI)     │
    └───────────────────────┘            └───────────────────────┘
                │                                     │
                │                                     │
      ✅ Reads .vscode/mcp.json              ❌ Cannot read .vscode/
         (workspace-local)                      (wrong context)
                │                                     │
                │                                     │
                ▼                                     ▼
    ┌───────────────────────┐            ┌───────────────────────┐
    │   MCP Servers Load    │            │  MCP Servers Missing  │
    │        ✓ ✓ ✓ ✓        │            │        ✗ ✗ ✗ ✗        │
    └───────────────────────┘            └───────────────────────┘
                                                      │
                                                      │ SOLUTION
                                                      ▼
                                         ┌───────────────────────────┐
                                         │ Create User-Level Config: │
                                         │ ~/.config/copilot/mcp.json│
                                         │           OR              │
                                         │ %APPDATA%\GitHub\Copilot\ │
                                         │        mcp.json           │
                                         └───────────────────────────┘
                                                      │
                                                      │
                                                      ▼
                                         ┌───────────────────────────┐
                                         │   MCP Servers Load        │
                                         │        ✓ ✓ ✓ ✓            │
                                         └───────────────────────────┘
```

## MCP Server Responsibilities

```
┌─────────────────────────────────────────────────────────────────┐
│                      MCP Server Ecosystem                        │
└─────────────────────────────────────────────────────────────────┘
           │
           ├─── sequential-thinking ──► Advanced reasoning & analysis
           │                             Used for: Complex problem solving
           │
           ├─── context7 ────────────► Library documentation lookup
           │                             Used for: API references, examples
           │
           ├─── memory ──────────────► Persistent context storage
           │                             Used for: Learning codebase patterns
           │
           └─── serena ──────────────► Code analysis & symbols
                                        Used for: Navigate code, understand structure
```

## Configuration Locations by Environment

### VS Code (Works Automatically)
```
MoneyBrain/
  └── .vscode/
      └── mcp.json  ◄── Loaded automatically by VS Code
```

### Copilot Workspace (Manual Setup Required)

**Linux/macOS:**
```
~/.config/
  └── copilot/
      └── mcp.json  ◄── Must create manually
```

**Windows:**
```
%APPDATA%\
  └── GitHub\
      └── Copilot\
          └── mcp.json  ◄── Must create manually
```

## Why Two Locations?

```
┌─────────────────────────────────────────────────────────────────┐
│                     Execution Context                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  VS Code Extension                  Copilot Workspace/CLI       │
│  ┌──────────────────┐              ┌──────────────────┐        │
│  │ Runs in:         │              │ Runs in:         │        │
│  │ - Local process  │              │ - CI/CD pipeline │        │
│  │ - User session   │              │ - Cloud worker   │        │
│  │ - Workspace dir  │              │ - Sandbox        │        │
│  │                  │              │                  │        │
│  │ Can access:      │              │ Can access:      │        │
│  │ ✓ .vscode/       │              │ ✗ .vscode/       │        │
│  │ ✓ Workspace      │              │ ✓ User config    │        │
│  └──────────────────┘              └──────────────────┘        │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Setup Workflow

```
[User has .vscode/mcp.json in VS Code]
              │
              ▼
[Tries GitHub Copilot Workspace]
              │
              ▼
[MCP servers don't load] ❌
              │
              ▼
[Read this documentation] 📖
              │
              ▼
[Copy config to user directory]
   • Linux/Mac: ~/.config/copilot/mcp.json
   • Windows: %APPDATA%\GitHub\Copilot\mcp.json
              │
              ▼
[Restart Copilot Workspace]
              │
              ▼
[MCP servers load successfully] ✅
```

## See Also

- **Quick Setup**: [MCP_QUICK_SETUP.md](MCP_QUICK_SETUP.md)
- **Full Guide**: [COPILOT_WORKSPACE_MCP_SETUP.md](COPILOT_WORKSPACE_MCP_SETUP.md)
- **Checklist**: [MCP_CONFIGURATION_CHECKLIST.md](MCP_CONFIGURATION_CHECKLIST.md)
