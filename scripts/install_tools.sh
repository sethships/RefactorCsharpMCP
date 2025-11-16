#!/usr/bin/env bash
set -euo pipefail

LOG_PREFIX="[install_tools]"

echo "$LOG_PREFIX Starting tool setup..."

# Only run this bootstrap in Claude Code's remote/web environment
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  echo "$LOG_PREFIX Not in Claude Code remote environment; skipping."
  exit 0
fi

# Use CLAUDE_PROJECT_DIR if set, otherwise use current directory
PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
TOOLS_DIR="$PROJECT_DIR/.tools"
mkdir -p "$TOOLS_DIR"

maybe_add_to_path() {
  local dir="$1"
  if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
    # Avoid duplicate lines
    if ! grep -q "$dir" "$CLAUDE_ENV_FILE" 2>/dev/null; then
      printf 'export PATH="%s:$PATH"\n' "$dir" >> "$CLAUDE_ENV_FILE"
    fi
  fi
}

echo "$LOG_PREFIX Running as: $(whoami)"

# --- Go: should already exist in Claude Code universal image ---
if command -v go >/dev/null 2>&1; then
  echo "$LOG_PREFIX Go present: $(go version)"
else
  echo "$LOG_PREFIX WARNING: go not found in PATH (unexpected in Claude Code web)."
fi

# --- .NET SDK: install to project-local dir if missing ---
if command -v dotnet >/dev/null 2>&1; then
  echo "$LOG_PREFIX dotnet already present: $(dotnet --version || echo 'version check failed')"
else
  DOTNET_ROOT="$TOOLS_DIR/dotnet"
  mkdir -p "$DOTNET_ROOT"
  TMP="$TOOLS_DIR/tmp"
  mkdir -p "$TMP"

  echo "$LOG_PREFIX Installing dotnet locally into $DOTNET_ROOT"

  if command -v curl >/dev/null 2>&1; then
    DOTNET_INSTALL_SH="$TOOLS_DIR/dotnet-install.sh"

    if curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$DOTNET_INSTALL_SH"; then
      chmod +x "$DOTNET_INSTALL_SH"

      # Use project-local temp dir to avoid /tmp issues
      # Install .NET 8.0 LTS (project targets net8.0)
      TMPDIR="$TMP" "$DOTNET_INSTALL_SH" --channel 8.0 --install-dir "$DOTNET_ROOT" || {
        echo "$LOG_PREFIX dotnet-install.sh failed; leaving dotnet unavailable for this session."
      }

      if [ -x "$DOTNET_ROOT/dotnet" ]; then
        maybe_add_to_path "$DOTNET_ROOT"

        if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
          printf 'export DOTNET_ROOT="%s"\n' "$DOTNET_ROOT" >> "$CLAUDE_ENV_FILE"
        fi

        echo "$LOG_PREFIX dotnet installed locally: $("$DOTNET_ROOT/dotnet" --version)"
      else
        echo "$LOG_PREFIX dotnet binary not found after install; check logs above."
      fi
    else
      echo "$LOG_PREFIX Unable to download dotnet-install.sh (network or domain blocked)."
    fi
  else
    echo "$LOG_PREFIX curl not available; cannot install dotnet."
  fi
fi

# --- GitHub CLI (gh): local install if missing ---
if command -v gh >/dev/null 2>&1; then
  echo "$LOG_PREFIX gh already present: $(gh --version | head -1 || echo 'version check failed')"
else
  GH_BIN_DIR="$TOOLS_DIR/gh"
  mkdir -p "$GH_BIN_DIR"

  if command -v curl >/dev/null 2>&1; then
    echo "$LOG_PREFIX Attempting local gh install..."
    GH_TGZ="$TOOLS_DIR/gh.tar.gz"

    # GitHub CLI version - update periodically from:
    # https://github.com/cli/cli/releases/latest
    GH_VERSION="2.83.1"
    if curl -fsSL \
      "https://github.com/cli/cli/releases/download/v${GH_VERSION}/gh_${GH_VERSION}_linux_amd64.tar.gz" \
      -o "$GH_TGZ"; then

      tar -xzf "$GH_TGZ" -C "$GH_BIN_DIR" --strip-components=1 || true

      if [ -x "$GH_BIN_DIR/bin/gh" ]; then
        maybe_add_to_path "$GH_BIN_DIR/bin"
        echo "$LOG_PREFIX gh installed locally: $("$GH_BIN_DIR/bin/gh" --version | head -1)"
      else
        echo "$LOG_PREFIX gh tarball extracted but gh binary not found at expected path."
      fi
    else
      echo "$LOG_PREFIX Unable to download gh (network or domain blocked)."
    fi
  else
    echo "$LOG_PREFIX curl not available; cannot install gh."
  fi
fi

echo "$LOG_PREFIX Setup complete (warnings above are non-fatal)."
exit 0
