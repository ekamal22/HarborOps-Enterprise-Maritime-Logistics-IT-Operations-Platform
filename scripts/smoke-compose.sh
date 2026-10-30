#!/usr/bin/env bash
set -euo pipefail

command -v docker >/dev/null || { echo "Docker required"; exit 1; }
command -v curl >/dev/null || { echo "curl required"; exit 1; }
command -v node >/dev/null || { echo "Node.js required"; exit 1; }

docker compose up -d --build

# The final Postgres image must run as its existing non-root account,
# without a gosu executable or compatibility symlink.
docker compose exec -T postgres sh -ec '
  test "$(id -u)" = "70"
  test ! -e /usr/local/bin/gosu
  test ! -e /usr/local/bin/su-exec
'

for i in $(seq 1 60); do
  if curl --silent --fail http://localhost:8080/health/ready >/dev/null; then break; fi
  if [[ "$i" == 60 ]]; then echo "Readiness timeout"; docker compose ps; exit 1; fi
  sleep 2
done

curl --fail --silent --show-error http://localhost:8080/health/live |
  node -e 'let s="";process.stdin.on("data",c=>s+=c).on("end",()=>{if(JSON.parse(s).status!=="healthy")process.exit(1)})'
curl --fail --silent --show-error http://localhost:8080/health/ready |
  node -e 'let s="";process.stdin.on("data",c=>s+=c).on("end",()=>{if(JSON.parse(s).status!=="healthy")process.exit(1)})'
curl --fail --silent --show-error http://localhost:8080/api/v1/system/baseline |
  node -e 'let s="";process.stdin.on("data",c=>s+=c).on("end",()=>{const x=JSON.parse(s);if(x.service!=="HarborOps"||x.datasetVersion!=="m0-synthetic-v1"||x.source!=="synthetic")process.exit(1)})'
curl --fail --silent --show-error http://localhost:5173/ |
  grep -q 'HarborOps'
echo "M0 API, dependency readiness, and frontend HTTP smoke checks passed"
