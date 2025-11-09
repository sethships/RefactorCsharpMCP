# Update Claude Code MCP Configuration to Use Custom Catalog
# This script backs up and updates the MCP configuration to use the custom catalog

$mcpConfigPath = "C:\Users\seth\.claude\mcp_servers.json"
$backupPath = "C:\Users\seth\.claude\mcp_servers.json.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"

Write-Host "Updating Claude Code MCP configuration..." -ForegroundColor Cyan

# Check if config file exists
if (-not (Test-Path $mcpConfigPath)) {
    Write-Host "✗ MCP configuration file not found at: $mcpConfigPath" -ForegroundColor Red
    exit 1
}

# Backup current configuration
Write-Host "`nCreating backup..." -ForegroundColor Cyan
Copy-Item $mcpConfigPath $backupPath
Write-Host "✓ Backup created: $backupPath" -ForegroundColor Green

# Read current configuration
$currentConfig = Get-Content $mcpConfigPath -Raw | ConvertFrom-Json

# Update MCP_DOCKER args to include custom catalog
if ($currentConfig.mcpServers.MCP_DOCKER) {
    Write-Host "`nUpdating MCP_DOCKER configuration..." -ForegroundColor Cyan

    # Check if --catalog is already in args
    $hasCustomCatalog = $currentConfig.mcpServers.MCP_DOCKER.args -contains "--catalog"

    if ($hasCustomCatalog) {
        Write-Host "⚠ Custom catalog already configured" -ForegroundColor Yellow

        # Find and update catalog name
        $catalogIndex = $currentConfig.mcpServers.MCP_DOCKER.args.IndexOf("--catalog")
        if ($catalogIndex -ge 0 -and $catalogIndex -lt ($currentConfig.mcpServers.MCP_DOCKER.args.Length - 1)) {
            $oldCatalog = $currentConfig.mcpServers.MCP_DOCKER.args[$catalogIndex + 1]
            Write-Host "Current catalog: $oldCatalog" -ForegroundColor Yellow

            if ($oldCatalog -ne "my-mcp-catalog") {
                Write-Host "Updating to: my-mcp-catalog" -ForegroundColor Cyan
                $currentConfig.mcpServers.MCP_DOCKER.args[$catalogIndex + 1] = "my-mcp-catalog"
            }
        }
    } else {
        Write-Host "Adding --catalog my-mcp-catalog to args" -ForegroundColor Cyan
        $currentConfig.mcpServers.MCP_DOCKER.args += @("--catalog", "my-mcp-catalog")
    }

    # Remove refactor-csharp-mcp standalone server if it exists
    if ($currentConfig.mcpServers."refactor-csharp-mcp") {
        Write-Host "`nRemoving standalone refactor-csharp-mcp server..." -ForegroundColor Yellow
        Write-Host "(It will now be accessible through MCP_DOCKER gateway)" -ForegroundColor Yellow
        $currentConfig.mcpServers.PSObject.Properties.Remove("refactor-csharp-mcp")
    }

    # Save updated configuration
    $currentConfig | ConvertTo-Json -Depth 10 | Set-Content $mcpConfigPath
    Write-Host "`n✓ Configuration updated successfully" -ForegroundColor Green

    # Show the updated configuration
    Write-Host "`nUpdated MCP_DOCKER configuration:" -ForegroundColor Cyan
    Write-Host ($currentConfig.mcpServers.MCP_DOCKER | ConvertTo-Json -Depth 5)

} else {
    Write-Host "✗ MCP_DOCKER not found in configuration" -ForegroundColor Red
    Write-Host "Restoring backup..." -ForegroundColor Yellow
    Copy-Item $backupPath $mcpConfigPath -Force
    exit 1
}

Write-Host "`n✓ Update complete!" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Restart Claude Code completely"
Write-Host "2. Verify connection with /mcp command"
Write-Host "3. Look for RefactorCsharpMCP tools with mcp__MCP_DOCKER__ prefix"
Write-Host "`nIf you need to restore the backup:" -ForegroundColor Yellow
Write-Host "  Copy-Item '$backupPath' '$mcpConfigPath' -Force"
