#!/usr/bin/env bash
# Development-only targeted .NET runner for M6. Not gate evidence.
# Usage: bash scripts/dev-m6-targeted.sh <project: unit|contract|integration> [dotnet test --filter expression]
# Starts one disposable, labelled PostgreSQL container, builds incrementally (the compiled output
# lives in the checkout's bin/obj, so unchanged projects are not rebuilt) and writes TRX and logs
# under $HARBOROPS_VERIFICATION_ROOT/dev, never under evidence.
set -euo pipefail
root=$(printenv HARBOROPS_VERIFICATION_ROOT || true)
case "$root" in
  [Ee]:[/\\]*|/e/*) ;;
  *) echo "HARBOROPS_VERIFICATION_ROOT must be on E:" >&2; exit 2 ;;
esac
kind="${1:?unit|contract|integration}"; filter="${2:-}"
case "$kind" in
  unit) project=tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj ;;
  contract) project=tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj ;;
  integration) project=tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj ;;
  *) echo "unknown project kind" >&2; exit 2 ;;
esac
name=ho-g6-dev-pg
stamp=$(date -u +%Y%m%dT%H%M%SZ)
out="$root/dev/$stamp-$kind"
mkdir -p "$out"
if MSYS_NO_PATHCONV=1 docker ps -aq --filter "name=^/$name\$" | grep -q .; then
  echo "Existing $name container: refusing takeover" >&2; exit 2
fi
winroot=$(cygpath -w "$root"); repo=$(pwd -W)
docker run -d --name "$name" --label harborops.g6=disposable --ulimit core=0 \
  --tmpfs /var/lib/postgresql/data:uid=70,gid=70,mode=0700 --cap-drop ALL \
  --security-opt no-new-privileges -e POSTGRES_DB=harborops_integration -e POSTGRES_USER=harborops \
  -e POSTGRES_HOST_AUTH_METHOD=trust harborops-postgres:16.15-rootless >/dev/null
trap 'docker rm -f "$name" >/dev/null 2>&1 || true' EXIT
for _ in $(seq 1 45); do
  docker exec "$name" pg_isready -U harborops -d harborops_integration >/dev/null 2>&1 && break
  sleep 1
done
cat > "$out/run.sh" <<'INNER'
set -uo pipefail
cd /src
dotnet build "$PROJECT" -c Release -v q 2>&1 | grep -E "error|Warn|Error" | sort -u | head -30
test "${PIPESTATUS[0]}" -eq 0 || exit 11
args=(--no-build -c Release --logger "trx;LogFileName=results.trx" --results-directory "$OUT")
if [ -n "${FILTER:-}" ]; then args+=(--filter "$FILTER"); fi
dotnet test "$PROJECT" "${args[@]}" 2>&1 | grep -v '^{"EventId' | tail -80
exit ${PIPESTATUS[0]}
INNER
set +e
MSYS_NO_PATHCONV=1 docker run --rm --ulimit core=0 --network "container:$name" -v "$repo:/src" \
  -v "$winroot:/cache" -w /src -e DOTNET_NOLOGO=1 -e NUGET_PACKAGES=/cache/nuget/packages \
  -e NUGET_HTTP_CACHE_PATH=/cache/nuget/http-cache -e DOTNET_CLI_HOME=/cache/dotnet-home \
  -e HOME=/cache/dotnet-home -e TMPDIR=/cache/tmp -e PROJECT="$project" -e FILTER="$filter" \
  -e OUT="/cache/dev/$stamp-$kind" \
  -e HARBOROPS_TEST_DB="Host=127.0.0.1;Port=5432;Database=harborops_integration;Username=harborops" \
  mcr.microsoft.com/dotnet/sdk:10.0 bash "/cache/dev/$stamp-$kind/run.sh" 2>&1 | tee "$out/console.log"
rc=${PIPESTATUS[0]}
echo "dev-run exit=$rc (development only, not gate evidence): $out"
exit "$rc"
