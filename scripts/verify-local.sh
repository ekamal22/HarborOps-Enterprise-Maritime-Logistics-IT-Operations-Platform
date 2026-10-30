#!/usr/bin/env bash
set -euo pipefail

command -v dotnet >/dev/null || { echo "Missing .NET SDK"; exit 1; }
command -v node >/dev/null || { echo "Missing Node.js"; exit 1; }
command -v npm >/dev/null || { echo "Missing npm"; exit 1; }

dotnet restore HarborOps.sln
dotnet build HarborOps.sln -c Release --no-restore
dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release --no-build
dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release --no-build

if [[ -n "${HARBOROPS_TEST_DB:-}" ]]; then
  dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release --no-build
else
  echo "Integration tests not executed: HARBOROPS_TEST_DB is unset"
fi

(cd web && npm install --no-audit --no-fund && npm test && npm run build)

if command -v docker >/dev/null; then
  docker compose config --quiet
else
  echo "Compose validation not executed: Docker unavailable"
fi
