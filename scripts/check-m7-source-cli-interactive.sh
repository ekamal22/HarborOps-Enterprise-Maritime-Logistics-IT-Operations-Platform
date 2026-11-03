#!/usr/bin/env bash
# Optional protected operator-terminal G7 lifecycle check. Never pipe or log bearer output.
set -euo pipefail
[[ -t 0 && -t 1 ]] || { echo "Interactive terminal required" >&2; exit 2; }
root_input="$(printenv HARBOROPS_VERIFICATION_ROOT || true)"
case "$root_input" in [Ee]:[/\\]*|/e/*) ;; *) echo "E: verification root required" >&2; exit 2 ;; esac
project="ho-verify-g7"
compose() { docker compose -p "$project" "$@"; }
sql() { compose exec -T postgres psql -U harborops -d harborops -Atc "$1"; }
org=$(sql "SELECT id FROM organizations WHERE slug='north-terminal'")
[[ "$org" =~ ^[0-9a-f-]{36}$ ]] || exit 2
code="g7_interactive_cli"
count=$(sql "SELECT count(*) FROM integration_sources WHERE organization_id='$org' AND source_code='$code'")
[[ "$count" == 0 ]] || { echo "Source already exists, refusing reuse" >&2; exit 2; }
echo "The next two commands show bearer credentials once. Keep this terminal private."
echo "Do not copy credentials into evidence or shell history."
read -r -p "Press Enter to create the disposable source credential: "
compose exec -it -e HARBOROPS_ALLOW_SOURCE_CLI=true api \
  dotnet HarborOps.Api.dll --source-cli create "$org" "$code" "G7 Operator CLI"
compose exec -T -e HARBOROPS_ALLOW_SOURCE_CLI=true api \
  dotnet HarborOps.Api.dll --source-cli inspect "$org" "$code"
first=$(sql "SELECT id FROM integration_credentials WHERE source_id=(SELECT id FROM integration_sources WHERE organization_id='$org' AND source_code='$code') ORDER BY created_at_utc,id LIMIT 1")
[[ "$first" =~ ^[0-9a-f-]{36}$ ]] || exit 1
read -r -p "Press Enter to rotate the credential: "
compose exec -it -e HARBOROPS_ALLOW_SOURCE_CLI=true api \
  dotnet HarborOps.Api.dll --source-cli rotate "$org" "$code"
active=$(sql "SELECT count(*) FROM integration_credentials WHERE organization_id='$org' AND source_id=(SELECT id FROM integration_sources WHERE organization_id='$org' AND source_code='$code') AND revoked_at_utc IS NULL AND expires_at_utc>now()")
[[ "$active" == 2 ]] || exit 1
compose exec -T -e HARBOROPS_ALLOW_SOURCE_CLI=true api \
  dotnet HarborOps.Api.dll --source-cli revoke "$org" "$code" "$first"
active=$(sql "SELECT count(*) FROM integration_credentials WHERE organization_id='$org' AND source_id=(SELECT id FROM integration_sources WHERE organization_id='$org' AND source_code='$code') AND revoked_at_utc IS NULL AND expires_at_utc>now()")
[[ "$active" == 1 ]] || exit 1
compose exec -T -e HARBOROPS_ALLOW_SOURCE_CLI=true api \
  dotnet HarborOps.Api.dll --source-cli disable "$org" "$code"
status=$(sql "SELECT status FROM integration_sources WHERE organization_id='$org' AND source_code='$code'")
active=$(sql "SELECT count(*) FROM integration_credentials WHERE organization_id='$org' AND source_id=(SELECT id FROM integration_sources WHERE organization_id='$org' AND source_code='$code') AND revoked_at_utc IS NULL")
[[ "$status" == "Disabled" && "$active" == 0 ]] || exit 1
compose exec -T -e HARBOROPS_ALLOW_SOURCE_CLI=true api \
  dotnet HarborOps.Api.dll --source-cli inspect "$org" "$code"
echo "Protected create, inspect, rotate, revoke and disable lifecycle completed."
