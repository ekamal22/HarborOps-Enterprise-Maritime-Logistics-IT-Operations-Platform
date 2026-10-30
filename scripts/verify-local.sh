#!/usr/bin/env bash
set -euo pipefail

for tool in dotnet node npm docker; do
  command -v "$tool" >/dev/null || { echo "Required verification tool unavailable: $tool" >&2; exit 1; }
done

if [[ ! -f web/package-lock.json ]]; then
  echo "web/package-lock.json is required for reproducible frontend verification" >&2
  echo "Generate it with npm install in web/, review and commit the file, then rerun verification" >&2
  exit 1
fi

if [[ -z "${HARBOROPS_TEST_DB:-}" ]]; then
  echo "HARBOROPS_TEST_DB must target a disposable PostgreSQL integration database" >&2
  exit 1
fi

dotnet restore HarborOps.sln
dotnet build HarborOps.sln -c Release --no-restore
dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release --no-build
dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release --no-build
dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build

(cd web && npm ci --no-audit --no-fund && npm test && npm run build)

docker compose config --quiet
echo "M0 local verification commands completed. Compose startup, browser checks, and failure injection remain separate gates."
