#!/usr/bin/env bash
# Sync curated docs into docs-site/content for VitePress.
# Source of truth remains docs/handbook, docs/runbooks, docs/agents/PROP_NUMBERING.md
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$ROOT/docs-site/content"
rm -rf "$DEST"
mkdir -p "$DEST/handbook" "$DEST/runbooks" "$DEST/agents"

cp -a "$ROOT/docs/handbook/." "$DEST/handbook/"
cp -a "$ROOT/docs/runbooks/." "$DEST/runbooks/"
cp "$ROOT/docs/agents/PROP_NUMBERING.md" "$DEST/agents/PROP_NUMBERING.md"

cat > "$DEST/index.md" <<'EOF'
---
layout: home
hero:
  name: SoulCore Handbook
  text: House Victoria
  tagline: Architecture, modules, workflows, and runbooks — searchable.
  actions:
    - theme: brand
      text: Architecture
      link: /handbook/architecture/overview
    - theme: alt
      text: Conventions
      link: /handbook/conventions
features:
  - title: Search
    details: Use the search box (top) for keywords across the handbook and runbooks.
  - title: Source of truth
    details: Edit docs/handbook and docs/runbooks in the repo, then run docs-site/sync-content.sh.
  - title: PROP registry
    details: Single PROP table lives at /agents/PROP_NUMBERING.
EOF

echo "Synced handbook + runbooks + PROP_NUMBERING → docs-site/content"
