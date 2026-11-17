#!/bin/bash
# Demonstrates that docker's label filter is an exact value match, so every container or network returned for
# label=com.docker.compose.project=<P> carries exactly P (this is why the in-script label comparison is unreachable).
set -u
IMG=alpine/socat:1.8.0.3
N=$(head -c4 /dev/urandom | od -An -tx1 | tr -d ' \n')
P="ho-verify-m17-demo-$N"
values=("$P" "${P}x" "${P^^}" "$P " " $P" "${P}-2" "ho-verify-m17-demo" "$P,extra" "${P}*" "")
i=0
for v in "${values[@]}"; do
  i=$((i+1))
  docker create --name "dec-$N-$i" --label "com.docker.compose.project=$v" --label com.docker.compose.service=s --entrypoint sleep $IMG 600 >/dev/null
  docker network create --label "com.docker.compose.project=$v" --label com.docker.compose.network=n "decnet-$N-$i" >/dev/null
done
echo "project under test: [$P]"
echo "--- containers returned by docker ps -a --filter label=com.docker.compose.project=$P"
for id in $(docker ps -a -q --no-trunc --filter "label=com.docker.compose.project=$P"); do
  docker inspect --format '{{.Name}} label=[{{index .Config.Labels "com.docker.compose.project"}}]' $id; done
echo "--- networks returned by docker network ls --filter label=com.docker.compose.project=$P"
for id in $(docker network ls -q --no-trunc --filter "label=com.docker.compose.project=$P"); do
  docker network inspect --format '{{.Name}} label=[{{index .Labels "com.docker.compose.project"}}]' $id; done
echo "--- all decoy labels created (for reference)"
for k in $(seq 1 $i); do docker inspect --format '{{.Name}} label=[{{index .Config.Labels "com.docker.compose.project"}}]' dec-$N-$k; done
for k in $(seq 1 $i); do docker rm "dec-$N-$k" >/dev/null; docker network rm "decnet-$N-$k" >/dev/null; done
echo "decoys removed by exact name"
