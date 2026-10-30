#!/usr/bin/env bash
set -euo pipefail

project=""
confirmed=false
while (($#)); do
  case "$1" in
    --project) project="${2:-}"; shift 2 ;;
    --confirm-disposable) confirmed=true; shift ;;
    *) echo "Usage: bash scripts/smoke-compose.sh --project ho-verify --confirm-disposable" >&2; exit 2 ;;
  esac
done

if [[ ! "$project" =~ ^ho-verify(-[a-z0-9-]+)?$ || "$confirmed" != true ]]; then
  echo "Only explicitly confirmed disposable ho-verify Compose projects are allowed" >&2
  exit 2
fi
for tool in docker curl node grep; do
  command -v "$tool" >/dev/null || { echo "Missing $tool" >&2; exit 1; }
done

compose=(docker compose --project-name "$project")
name=$("${compose[@]}" config --format json | node -e '
let s=""; process.stdin.on("data",x=>s+=x).on("end",()=>{
  const cfg=JSON.parse(s);
  if(!cfg.services?.postgres || !cfg.services?.migrator || !cfg.services?.api || !cfg.services?.web)
    process.exit(1);
  process.stdout.write(cfg.name);
});')
[[ "$name" == "$project" ]] || { echo "Unexpected Compose configuration" >&2; exit 1; }

command -v git >/dev/null || { echo "Git required for revision-bound image builds" >&2; exit 1; }
export HARBOROPS_SOURCE_SHA=$(git rev-parse HEAD)
if [[ ! "$HARBOROPS_SOURCE_SHA" =~ ^[0-9a-f]{40}$ ]]; then
  echo "Cannot identify repository source revision" >&2; exit 1
fi
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Tracked source changes are uncommitted; cannot label reproducible images" >&2; exit 1
fi

"${compose[@]}" up -d --build
for service in postgres api web; do
  id=$("${compose[@]}" ps -q "$service")
  [[ -n "$id" ]] || { echo "$service did not start" >&2; exit 1; }
  label=$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' "$id")
  [[ "$label" == "$project" ]] || { echo "Unexpected $service container ownership" >&2; exit 1; }
  image=$(docker inspect --format '{{.Image}}' "$id")
  revision=$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image")
  [[ "$revision" == "$HARBOROPS_SOURCE_SHA" ]] || { echo "Stale $service image built for $revision" >&2; exit 1; }
done

"${compose[@]}" exec -T postgres sh -ec '
  test "$(id -u)" = "70"
  test ! -e /usr/local/bin/gosu
  test ! -e /usr/local/bin/su-exec
'

api=$("${compose[@]}" port api 8080)
web=$("${compose[@]}" port web 80)
[[ "$api" =~ ^127\.0\.0\.1:[0-9]+$ ]] || { echo "API must be bound to loopback" >&2; exit 1; }
[[ "$web" =~ ^127\.0\.0\.1:[0-9]+$ ]] || { echo "Web must be bound to loopback" >&2; exit 1; }
api="http://$api"
web="http://$web"

for i in $(seq 1 60); do
  if curl --silent --fail --max-time 5 "$api/health/ready" >/dev/null; then break; fi
  if [[ "$i" == 60 ]]; then echo "Readiness timeout" >&2; "${compose[@]}" ps; exit 1; fi
  sleep 2
done

curl --fail --silent --show-error "$api/health/live" |
  node -e 'let s="";process.stdin.on("data",x=>s+=x).on("end",()=>{if(JSON.parse(s).status!=="healthy")process.exit(1)})'
curl --fail --silent --show-error "$api/health/ready" |
  node -e 'let s="";process.stdin.on("data",x=>s+=x).on("end",()=>{if(JSON.parse(s).status!=="healthy")process.exit(1)})'
curl --fail --silent --show-error "$api/api/v1/system/baseline" |
  node -e 'let s="";process.stdin.on("data",x=>s+=x).on("end",()=>{const d=JSON.parse(s);if(d.service!=="HarborOps"||d.datasetVersion!=="m0-synthetic-v1"||d.source!=="synthetic")process.exit(1)})'
curl --fail --silent --show-error "$web/" | grep -q 'HarborOps'

echo "M0 Compose smoke checks passed on isolated project $project"
