#!/usr/bin/env bash
# Read-only privilege review against the exclusively disposable G16 Compose DB.
set -euo pipefail
[[ "${COMPOSE_FILE:-}" == *"compose.g16.verify.yaml" ]] ||
  { echo "G16 Compose file required" >&2; exit 2; }
[[ "${HARBOROPS_SOURCE_SHA:-}" =~ ^[a-f0-9]{40}$ ]] ||
  { echo "Source-lock required" >&2; exit 2; }
docker compose -p ho-verify-g16 exec -T postgres \
  psql -X -v ON_ERROR_STOP=1 -U harborops -d harborops <<'SQL'
DO $check$
DECLARE
    api oid;
    worker oid;
BEGIN
    SELECT oid INTO api FROM pg_roles WHERE rolname='harborops_api';
    SELECT oid INTO worker FROM pg_roles WHERE rolname='harborops_worker';
    IF api IS NULL OR worker IS NULL THEN
        RAISE EXCEPTION 'M16 runtime database identity missing';
    END IF;
    IF EXISTS (
        SELECT 1 FROM pg_roles
         WHERE oid IN (api,worker)
           AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication)
    ) THEN
        RAISE EXCEPTION 'M16 runtime role has privileged flags';
    END IF;
    IF NOT has_table_privilege('harborops_api','public.audit_entries','SELECT')
       OR has_table_privilege('harborops_api','public.audit_entries','INSERT')
       OR has_table_privilege('harborops_api','public.audit_entries','UPDATE')
       OR has_table_privilege('harborops_api','public.audit_entries','DELETE')
       OR has_table_privilege('harborops_worker','public.audit_entries','SELECT')
       OR has_table_privilege('harborops_worker','public.audit_entries','INSERT')
       OR has_schema_privilege('harborops_api','public','CREATE')
       OR has_schema_privilege('harborops_worker','public','CREATE')
       OR NOT has_function_privilege('harborops_api',
           'public.m16_record_audit_access(uuid,character varying,uuid,uuid,uuid,character varying)',
           'EXECUTE')
       OR has_function_privilege('harborops_worker',
           'public.m16_record_audit_access(uuid,character varying,uuid,uuid,uuid,character varying)',
           'EXECUTE')
    THEN
        RAISE EXCEPTION 'M16 role grant contract violated';
    END IF;
END $check$;
SELECT 'M16 least-privilege role matrix PASS';
SQL
