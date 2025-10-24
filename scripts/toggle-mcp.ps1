# Toggle RefactorCsharpMCP server on/off in Claude Code
# Usage:
#   .\toggle-mcp.ps1 on     - Enable the MCP server
#   .\toggle-mcp.ps1 off    - Disable the MCP server
#   .\toggle-mcp.ps1 status - Show current status

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("on", "off", "status")]
    [string]$Action
)

$serverName = "refactor-csharp-mcp"
$serverPath = "C:\src\DevTools\RefactorCsharpMCP\publish\RefactorCsharpMCP.Server.exe"

# Check current status
$currentServers = & claude mcp list | Out-String
$isEnabled = $currentServers -match $serverName

if ($Action -eq "status") {
    if ($isEnabled) {
        Write-Host "RefactorCsharpMCP server is ENABLED" -ForegroundColor Green
    } else {
        Write-Host "RefactorCsharpMCP server is DISABLED" -ForegroundColor Yellow
    }
    exit 0
}

if ($Action -eq "on") {
    if ($isEnabled) {
        Write-Host "RefactorCsharpMCP server is already enabled." -ForegroundColor Yellow
    } else {
        Write-Host "Enabling RefactorCsharpMCP server..." -ForegroundColor Green
        & claude mcp add $serverName $serverPath
        Write-Host "MCP server enabled. Restart Claude Code to load it." -ForegroundColor Yellow
    }
} else {
    if (-not $isEnabled) {
        Write-Host "RefactorCsharpMCP server is already disabled." -ForegroundColor Yellow
    } else {
        Write-Host "Disabling RefactorCsharpMCP server..." -ForegroundColor Yellow
        & claude mcp remove $serverName
        Write-Host "MCP server disabled. Restart Claude Code to unload it." -ForegroundColor Green
    }
}
