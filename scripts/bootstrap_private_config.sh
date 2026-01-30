#!/bin/bash

###############################################################################
# bootstrap_private_config.sh
#
# This script bootstraps private Claude Code configurations from a remote
# private repository into a project's .claude/ directory.
#
# Usage: ./bootstrap_private_config.sh
#
# Required Environment Variables:
#   PRIVATE_CLAUDE_CONFIG_REPO_URL - URL to the private config repository
#                                    (e.g., https://github.com/user/ClaudeCodeConfigs)
#
# Optional Environment Variables:
#   PRIVATE_CONFIG_BRANCH - Branch to use (default: main)
#   PRIVATE_CONFIG_DIR - Where to clone config (default: .claude/private-config)
#   GH_TOKEN - GitHub token for private repo access (auto-injected if available)
#
# What this script does:
#   1. Clones/pulls private config repo into .claude/private-config/
#   2. Merges settings.private.json with project .claude/settings.json
#   3. Merges mcp.private.json with project .mcp.json
#   4. Creates .claude/settings.local.json with merged result
#   5. Symlinks global commands and skills for web environments
#   6. Logs all operations for debugging
#
# Exit codes:
#   0 - Success
#   1 - Missing required environment variables
#   2 - Git clone/pull failed
#   3 - JSON merge failed
#   4 - Required tools missing (git, jq)
###############################################################################

set -e  # Exit on error
set -o pipefail  # Catch errors in pipes

# Configuration
PRIVATE_CONFIG_BRANCH="${PRIVATE_CONFIG_BRANCH:-main}"
PRIVATE_CONFIG_DIR="${PRIVATE_CONFIG_DIR:-.claude/private-config}"
BOOTSTRAP_SUBDIR="bootstrap"
LOG_PREFIX="[bootstrap-private-config]"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

###############################################################################
# Logging functions
###############################################################################

log_info() {
    echo -e "${BLUE}${LOG_PREFIX} ℹ${NC} $1"
}

log_success() {
    echo -e "${GREEN}${LOG_PREFIX} ✓${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}${LOG_PREFIX} ⚠${NC} $1"
}

log_error() {
    echo -e "${RED}${LOG_PREFIX} ✗${NC} $1" >&2
}

###############################################################################
# Validation functions
###############################################################################

check_required_tools() {
    local missing_tools=()

    if ! command -v git &> /dev/null; then
        missing_tools+=("git")
    fi

    if ! command -v jq &> /dev/null; then
        missing_tools+=("jq")
    fi

    if [ ${#missing_tools[@]} -gt 0 ]; then
        log_error "Required tools not found: ${missing_tools[*]}"
        log_error "Please install missing tools and try again"
        return 4
    fi

    log_success "Required tools available (git, jq)"
    return 0
}

load_env_file() {
    # Load environment variables from .env file if it exists
    # This allows users to store PRIVATE_CLAUDE_CONFIG_REPO_URL in .env
    if [ -f ".env" ]; then
        log_info "Loading environment from .env file..."
        # Export variables from .env, ignoring comments and empty lines
        set -a
        # shellcheck disable=SC1091
        source .env 2>/dev/null || true
        set +a
        log_success "Environment file loaded"
    fi
}

check_environment_variables() {
    # First try to load from .env file
    load_env_file

    if [ -z "$PRIVATE_CLAUDE_CONFIG_REPO_URL" ]; then
        # Not an error - gracefully skip private config bootstrap
        log_info "PRIVATE_CLAUDE_CONFIG_REPO_URL not set - skipping private config bootstrap"
        log_info "This is normal for public repository users without private config"
        log_info "To enable: set PRIVATE_CLAUDE_CONFIG_REPO_URL in .env or environment"
        return 100  # Special exit code indicating graceful skip
    fi

    log_success "Environment variables validated"
    log_info "Config repo: $PRIVATE_CLAUDE_CONFIG_REPO_URL"
    log_info "Branch: $PRIVATE_CONFIG_BRANCH"
    return 0
}

###############################################################################
# Git operations
###############################################################################

build_authenticated_url() {
    # Build an authenticated GitHub URL using GH_TOKEN if available
    # Usage: build_authenticated_url <url>
    local url="$1"

    # If GH_TOKEN is available, inject it into the URL for authentication
    if [ -n "$GH_TOKEN" ]; then
        # Replace https:// with https://oauth2:TOKEN@
        echo "$url" | sed "s|https://|https://oauth2:${GH_TOKEN}@|"
    else
        # Return original URL if no token available
        echo "$url"
    fi
}

clone_or_update_config_repo() {
    log_info "Syncing private configuration repository..."

    # Validate repository URL format
    if ! echo "$PRIVATE_CLAUDE_CONFIG_REPO_URL" | grep -qE '^https://'; then
        log_error "Repository URL must use HTTPS protocol for security"
        log_error "Provided URL: $PRIVATE_CLAUDE_CONFIG_REPO_URL"
        log_error "Expected format: https://github.com/user/repo"
        return 2
    fi

    # Build authenticated URL for git operations
    local auth_url=$(build_authenticated_url "$PRIVATE_CLAUDE_CONFIG_REPO_URL")

    if [ -d "$PRIVATE_CONFIG_DIR/.git" ]; then
        log_info "Config directory exists, pulling latest changes..."

        # Save current directory
        local current_dir=$(pwd)

        cd "$PRIVATE_CONFIG_DIR"

        # Update remote URL to use authenticated URL (in case token changed)
        git remote set-url origin "$auth_url" &> /dev/null

        # Fetch and reset to latest
        if git fetch origin "$PRIVATE_CONFIG_BRANCH" &> /dev/null && \
           git reset --hard "origin/$PRIVATE_CONFIG_BRANCH" &> /dev/null; then
            log_success "Private config updated to latest"
        else
            log_warning "Failed to update private config, using existing version"
        fi

        cd "$current_dir"
    else
        log_info "Cloning private configuration repository..."

        # Ensure parent directory exists
        mkdir -p "$(dirname "$PRIVATE_CONFIG_DIR")"

        if git clone --depth 1 --branch "$PRIVATE_CONFIG_BRANCH" \
                     "$auth_url" "$PRIVATE_CONFIG_DIR" &> /dev/null; then
            log_success "Private config repository cloned"
        else
            log_error "Failed to clone private config repository"
            log_error "URL: $PRIVATE_CLAUDE_CONFIG_REPO_URL"
            log_error "Branch: $PRIVATE_CONFIG_BRANCH"
            return 2
        fi
    fi

    return 0
}

###############################################################################
# JSON merge functions
###############################################################################

merge_json_arrays() {
    # Merge two JSON arrays, removing duplicates
    # Usage: merge_json_arrays array1 array2
    local arr1="$1"
    local arr2="$2"

    echo "$arr1 $arr2" | jq -s 'add | unique'
}

merge_permission_lists() {
    # Merge permission allow/ask/deny lists with de-duplication
    # Usage: merge_permission_lists base_json private_json permission_type
    local base_json="$1"
    local private_json="$2"
    local perm_type="$3"  # "allow", "ask", or "deny"

    local base_perms=$(echo "$base_json" | jq -r ".permissions.$perm_type // []")
    local private_perms=$(echo "$private_json" | jq -r ".permissions.$perm_type // []")

    merge_json_arrays "$base_perms" "$private_perms"
}

merge_settings() {
    local project_settings="$1"
    local private_settings="$2"
    local output_settings="$3"

    log_info "Merging settings files..."

    # Read JSON files
    local project_json="{}"
    if [ -f "$project_settings" ]; then
        project_json=$(cat "$project_settings")
        log_info "Found project settings: $project_settings"
    else
        log_info "No project settings found, using defaults"
    fi

    if [ ! -f "$private_settings" ]; then
        log_error "Private settings not found: $private_settings"
        return 3
    fi

    # Validate JSON structure before merging
    log_info "Validating private settings JSON structure..."
    if ! jq -e '.permissions | has("allow", "ask", "deny")' "$private_settings" > /dev/null 2>&1; then
        log_error "Invalid private settings structure - missing required permission fields"
        log_error "Expected: .permissions.allow, .permissions.ask, .permissions.deny"
        return 3
    fi

    # Validate no dangerous patterns in allow list
    if jq -e '.permissions.allow[]?' "$private_settings" 2>/dev/null | grep -qE '(sudo.*rm.*-rf|mkfs|dd.*if=/dev/zero|format.*C:|del.*\/s|rmdir.*\/s)'; then
        log_error "Dangerous patterns detected in private settings allow list"
        log_error "Refusing to merge settings with potentially destructive permissions"
        return 3
    fi

    log_success "Private settings validation passed"

    local private_json=$(cat "$private_settings")

    # Merge permissions with union strategy
    local merged_allow=$(merge_permission_lists "$project_json" "$private_json" "allow")
    local merged_ask=$(merge_permission_lists "$project_json" "$private_json" "ask")
    local merged_deny=$(merge_permission_lists "$project_json" "$private_json" "deny")

    # Merge enabled MCP servers (union)
    local project_mcp=$(echo "$project_json" | jq -r '.enabledMcpjsonServers // []')
    local private_mcp=$(echo "$private_json" | jq -r '.enabledMcpjsonServers // []')
    local merged_mcp=$(merge_json_arrays "$project_mcp" "$private_mcp")

    # Merge allowed tools (union)
    local project_tools=$(echo "$project_json" | jq -r '.allowedTools // []')
    local private_tools=$(echo "$private_json" | jq -r '.allowedTools // []')
    local merged_tools=$(merge_json_arrays "$project_tools" "$private_tools")

    # Build final merged JSON
    # Start with project settings as base, then overlay private settings
    # IMPORTANT: Exclude hooks section - hooks should only be in settings.json (source of truth)
    local merged_json=$(echo "$project_json" "$private_json" | jq -s '
        .[0] * .[1] |
        .permissions.allow = '"$merged_allow"' |
        .permissions.ask = '"$merged_ask"' |
        .permissions.deny = '"$merged_deny"' |
        .enabledMcpjsonServers = '"$merged_mcp"' |
        .allowedTools = '"$merged_tools"' |
        del(.hooks)
    ')

    # Write merged settings
    echo "$merged_json" | jq '.' > "$output_settings"

    log_success "Settings merged: $output_settings"
    log_info "  - Permission allow rules: $(echo "$merged_allow" | jq 'length')"
    log_info "  - Permission ask rules: $(echo "$merged_ask" | jq 'length')"
    log_info "  - Permission deny rules: $(echo "$merged_deny" | jq 'length')"
    log_info "  - Enabled MCP servers: $(echo "$merged_mcp" | jq 'length')"
    log_info "  - Allowed tools: $(echo "$merged_tools" | jq 'length')"

    return 0
}

merge_mcp_config() {
    local project_mcp=".mcp.json"
    local private_mcp="$PRIVATE_CONFIG_DIR/$BOOTSTRAP_SUBDIR/mcp.private.json"
    local output_mcp=".mcp.json"

    log_info "Merging MCP configurations..."

    # Read JSON files
    local project_json="{\"mcpServers\": {}}"
    if [ -f "$project_mcp" ]; then
        project_json=$(cat "$project_mcp")
        log_info "Found project MCP config: $project_mcp"
    else
        log_info "No project MCP config found, using defaults"
    fi

    if [ ! -f "$private_mcp" ]; then
        log_warning "Private MCP config not found: $private_mcp (skipping)"
        return 0
    fi

    local private_json=$(cat "$private_mcp")

    # Merge MCP servers (project-specific servers override private servers with same name)
    local merged_json=$(echo "$project_json" "$private_json" | jq -s '
        .[1].mcpServers as $private |
        .[0].mcpServers as $project |
        .[0] |
        .mcpServers = ($private + $project)
    ')

    # Write merged MCP config
    echo "$merged_json" | jq '.' > "$output_mcp"

    log_success "MCP config merged: $output_mcp"
    log_info "  - MCP servers configured: $(echo "$merged_json" | jq '.mcpServers | length')"

    return 0
}

symlink_global_resources() {
    # Symlink global commands and skills for web environments
    # In local environments, these would be in ~/.claude/ instead
    log_info "Symlinking global commands and skills..."

    local global_dir="$PRIVATE_CONFIG_DIR/global/.claude"
    local symlinks_created=0

    # Create .claude/commands directory if it doesn't exist
    mkdir -p .claude/commands

    # Symlink each command from global config
    if [ -d "$global_dir/commands" ]; then
        for cmd_file in "$global_dir/commands"/*.md; do
            if [ -f "$cmd_file" ]; then
                local cmd_name=$(basename "$cmd_file")
                local target=".claude/commands/$cmd_name"

                # Remove existing symlink/file if it exists
                rm -f "$target"

                # Create relative symlink
                ln -s "../private-config/global/.claude/commands/$cmd_name" "$target"
                ((symlinks_created++))
            fi
        done
    fi

    # Create .claude/skills directory if it doesn't exist
    mkdir -p .claude/skills

    # Symlink each skill from global config
    if [ -d "$global_dir/skills" ]; then
        for skill_dir in "$global_dir/skills"/*; do
            if [ -d "$skill_dir" ]; then
                local skill_name=$(basename "$skill_dir")
                local target=".claude/skills/$skill_name"

                # Remove existing symlink/directory if it exists
                rm -rf "$target"

                # Create relative symlink
                ln -s "../private-config/global/.claude/skills/$skill_name" "$target"
                ((symlinks_created++))
            fi
        done
    fi

    log_success "Created $symlinks_created symlinks for global resources"

    return 0
}

###############################################################################
# Main execution
###############################################################################

main() {
    # Set trap to clean up on error
    trap 'log_error "Bootstrap failed - cleaning up partial state"; rm -f .claude/settings.local.json.tmp .mcp.json.tmp 2>/dev/null; exit 1' ERR

    log_info "Starting private configuration bootstrap..."
    log_info "Working directory: $(pwd)"

    # Validate prerequisites
    check_required_tools || exit $?

    # Check environment variables - exit code 100 means graceful skip
    check_environment_variables
    local env_result=$?
    if [ $env_result -eq 100 ]; then
        log_success "Private config bootstrap skipped (no config URL set)"
        exit 0  # Exit with success - this is expected for public repo users
    elif [ $env_result -ne 0 ]; then
        exit $env_result
    fi

    # Clone or update config repo
    clone_or_update_config_repo || exit $?

    # Ensure .claude directory exists
    mkdir -p .claude

    # Merge settings
    merge_settings \
        ".claude/settings.json" \
        "$PRIVATE_CONFIG_DIR/$BOOTSTRAP_SUBDIR/settings.private.json" \
        ".claude/settings.local.json" || exit $?

    # Merge MCP config
    merge_mcp_config || exit $?

    # Symlink global commands and skills (for web environments)
    symlink_global_resources || exit $?

    log_success "Private configuration bootstrap complete!"
    echo ""
    log_info "Next steps:"
    log_info "  1. Ensure your project CLAUDE.md imports private config:"
    log_info "     @$PRIVATE_CONFIG_DIR/$BOOTSTRAP_SUBDIR/CLAUDE.private.md"
    log_info "  2. Set required MCP environment variables"
    log_info "  3. Verify .claude/settings.local.json has expected permissions"
    log_info "  4. Restart Claude Code session to load new commands and skills"
    echo ""

    return 0
}

# Run main function
main "$@"
