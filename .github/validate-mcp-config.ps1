# MCP Configuration Validation Script (PowerShell)
# Checks if MCP servers are properly configured for GitHub Copilot Workspace

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "MCP Configuration Validator" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

$script:Errors = 0
$script:Warnings = 0

function Check-Command {
    param($CommandName)
    
    if (Get-Command $CommandName -ErrorAction SilentlyContinue) {
        Write-Host "✓ $CommandName is installed" -ForegroundColor Green
        return $true
    } else {
        Write-Host "✗ $CommandName is NOT installed" -ForegroundColor Red
        $script:Errors++
        return $false
    }
}

function Check-File {
    param($FilePath)
    
    if (Test-Path $FilePath) {
        Write-Host "✓ Found: $FilePath" -ForegroundColor Green
        return $true
    } else {
        Write-Host "✗ Missing: $FilePath" -ForegroundColor Red
        $script:Errors++
        return $false
    }
}

function Validate-Json {
    param($FilePath)
    
    try {
        $null = Get-Content $FilePath | ConvertFrom-Json
        Write-Host "✓ Valid JSON: $FilePath" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "✗ Invalid JSON: $FilePath" -ForegroundColor Red
        $script:Errors++
        return $false
    }
}

Write-Host "1. Checking prerequisites..." -ForegroundColor Yellow
Write-Host "------------------------------"
Check-Command "node"
Check-Command "npm"
Check-Command "npx"
Check-Command "python"
Check-Command "uvx"

Write-Host ""
Write-Host "2. Checking VS Code configuration..." -ForegroundColor Yellow
Write-Host "------------------------------"
if (Test-Path ".vscode\mcp.json") {
    Check-File ".vscode\mcp.json"
    Validate-Json ".vscode\mcp.json"
} else {
    Write-Host "⚠ .vscode\mcp.json not found (run from repository root)" -ForegroundColor Yellow
    $script:Warnings++
}

Write-Host ""
Write-Host "3. Checking Copilot Workspace configuration..." -ForegroundColor Yellow
Write-Host "------------------------------"

$ConfigPath = "$env:APPDATA\GitHub\Copilot\mcp.json"
Write-Host "OS: Windows"
Write-Host "Expected config: $ConfigPath"

if (Test-Path $ConfigPath) {
    Check-File $ConfigPath
    Validate-Json $ConfigPath
    
    # Check if it matches the repository version
    if (Test-Path ".vscode\mcp.json") {
        $repoContent = Get-Content ".vscode\mcp.json" -Raw
        $userContent = Get-Content $ConfigPath -Raw
        
        if ($repoContent -eq $userContent) {
            Write-Host "✓ User config matches repository config" -ForegroundColor Green
        } else {
            Write-Host "⚠ User config differs from repository config" -ForegroundColor Yellow
            $script:Warnings++
        }
    }
} else {
    Write-Host "✗ Missing: $ConfigPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "To fix, run in PowerShell:"
    Write-Host '  New-Item -ItemType Directory -Force -Path "$env:APPDATA\GitHub\Copilot"'
    Write-Host '  Copy-Item -Path ".vscode\mcp.json" -Destination "$env:APPDATA\GitHub\Copilot\mcp.json"'
    $script:Errors++
}

Write-Host ""
Write-Host "4. Checking MCP server definitions..." -ForegroundColor Yellow
Write-Host "------------------------------"

if (Test-Path $ConfigPath) {
    $config = Get-Content $ConfigPath -Raw
    $servers = @("sequential-thinking", "context7", "memory", "serena")
    
    foreach ($server in $servers) {
        if ($config -match "`"$server`"") {
            Write-Host "✓ Server defined: $server" -ForegroundColor Green
        } else {
            Write-Host "✗ Server missing: $server" -ForegroundColor Red
            $script:Errors++
        }
    }
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Validation Summary" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Errors: $script:Errors" -ForegroundColor $(if ($script:Errors -eq 0) { "Green" } else { "Red" })
Write-Host "Warnings: $script:Warnings" -ForegroundColor $(if ($script:Warnings -eq 0) { "Green" } else { "Yellow" })
Write-Host ""

if ($script:Errors -eq 0 -and $script:Warnings -eq 0) {
    Write-Host "✓ Configuration is complete and valid!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "1. Restart GitHub Copilot Workspace"
    Write-Host "2. Test MCP servers by asking Copilot to use them"
    exit 0
} elseif ($script:Errors -eq 0) {
    Write-Host "⚠ Configuration is valid but has warnings" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Review warnings above and address them if needed."
    exit 0
} else {
    Write-Host "✗ Configuration has errors that need to be fixed" -ForegroundColor Red
    Write-Host ""
    Write-Host "See .github\COPILOT_WORKSPACE_MCP_SETUP.md for detailed setup instructions."
    exit 1
}
