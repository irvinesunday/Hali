#!/usr/bin/env bash
# Hali — Dispatch arch doc to docs/arch/ via GitHub Contents API
# Run from WSL: bash dispatch.sh
# Requires: GITHUB_TOKEN set in environment (or replace below)
# Repo:      github.com/irvinesunday/Hali

set -euo pipefail

REPO="irvinesunday/Hali"
FILE_PATH="docs/arch/hali_institution_dashboard_canonical_spec.md"
BRANCH="main"
TOKEN="${GITHUB_TOKEN:-}"   # Set via: export GITHUB_TOKEN=ghp_...

if [[ -z "$TOKEN" ]]; then
  echo "❌  GITHUB_TOKEN is not set."
  echo "    Run: export GITHUB_TOKEN=ghp_..."
  exit 1
fi

# Check if the file already exists (needed to supply sha for updates)
echo "🔍  Checking if $FILE_PATH already exists in $REPO..."
EXISTING=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/vnd.github+json" \
  "https://api.github.com/repos/$REPO/contents/$FILE_PATH?ref=$BRANCH")

if [[ "$EXISTING" == "200" ]]; then
  echo "ℹ️   File exists — fetching SHA for update..."
  SHA=$(curl -s \
    -H "Authorization: Bearer $TOKEN" \
    -H "Accept: application/vnd.github+json" \
    "https://api.github.com/repos/$REPO/contents/$FILE_PATH?ref=$BRANCH" \
    | python3 -c "import sys, json; print(json.load(sys.stdin)['sha'])")
  echo "    SHA: $SHA"
  SHA_FIELD=", \"sha\": \"$SHA\""
else
  echo "ℹ️   File does not exist — creating new."
  SHA_FIELD=""
fi

# Base64-encode the spec file
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SPEC_FILE="$SCRIPT_DIR/hali_institution_dashboard_canonical_spec_v1.1.md"

if [[ ! -f "$SPEC_FILE" ]]; then
  echo "❌  Spec file not found: $SPEC_FILE"
  echo "    Copy the spec file alongside this script and retry."
  exit 1
fi

CONTENT=$(base64 -w 0 "$SPEC_FILE")

COMMIT_MSG="docs(arch): add institution dashboard canonical spec v1.1

Adds the corrected and hardened institution dashboard canonical spec
for Phase 2 MVP. Key additions over v1.0:

- Phase 2 placement and hard gate condition (§0)
- Auth model for institution users — mandatory 2FA requirement (§3.5)
- institution_response_stage enum + schema addition (§11.7)
- API dependency tables per page (§8.7, §9.7, §10.9, §13.6, §14.6)
- Trend computation basis tied to SDS trajectory (§9.3)
- Notification bell corrected as in-app alerts, not Expo push (§12)
- institutions/alerts endpoint added to institution API surface (§12.5)
- search endpoint resolved via q= param on clusters endpoint (§15.4)
- Avg First Response marked as derived metric (§14.3)
- §4.1 institution list reframed as seed data
- Demo mode production boundary isolation requirement (§16.1)"

echo "🚀  Dispatching to $FILE_PATH on branch $BRANCH..."
RESPONSE=$(curl -s -w "\n%{http_code}" \
  -X PUT \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/vnd.github+json" \
  -H "Content-Type: application/json" \
  "https://api.github.com/repos/$REPO/contents/$FILE_PATH" \
  -d "{
    \"message\": $(python3 -c "import json, sys; print(json.dumps(sys.argv[1]))" "$COMMIT_MSG"),
    \"content\": \"$CONTENT\",
    \"branch\": \"$BRANCH\"
    $SHA_FIELD
  }")

HTTP_CODE=$(echo "$RESPONSE" | tail -1)
BODY=$(echo "$RESPONSE" | sed '$d')

if [[ "$HTTP_CODE" == "200" || "$HTTP_CODE" == "201" ]]; then
  COMMIT_SHA=$(echo "$BODY" | python3 -c "import sys, json; print(json.load(sys.stdin)['commit']['sha'])" 2>/dev/null || echo "unknown")
  echo ""
  echo "✅  Success — HTTP $HTTP_CODE"
  echo "    Commit: $COMMIT_SHA"
  echo "    View:   https://github.com/$REPO/blob/$BRANCH/$FILE_PATH"
else
  echo ""
  echo "❌  Failed — HTTP $HTTP_CODE"
  echo "$BODY" | python3 -m json.tool 2>/dev/null || echo "$BODY"
  exit 1
fi
