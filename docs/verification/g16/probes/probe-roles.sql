-- G16 independent database privilege probe (verification only, disposable DB only).
-- Run once per login role with psql -X -v ON_ERROR_STOP=0. Every probe reports
-- ALLOWED or DENIED with SQLSTATE so the result is observable evidence.
\set QUIET on
\pset format unaligned
SELECT 'ROLE ' || current_user || ' session_user ' || session_user AS identity;
SELECT 'FLAGS super=' || rolsuper || ' createdb=' || rolcreatedb || ' createrole=' || rolcreaterole
    || ' repl=' || rolreplication || ' bypassrls=' || rolbypassrls AS flags
  FROM pg_roles WHERE rolname = current_user;

CREATE OR REPLACE FUNCTION pg_temp.probe(name text, stmt text) RETURNS void LANGUAGE plpgsql AS $p$
BEGIN
    BEGIN
        EXECUTE stmt;
        RAISE NOTICE 'PROBE % -> ALLOWED', name;
        RAISE EXCEPTION 'probe_rollback' USING ERRCODE = 'P0001';
    EXCEPTION WHEN OTHERS THEN
        IF SQLERRM = 'probe_rollback' THEN
            NULL;
        ELSE
            RAISE NOTICE 'PROBE % -> DENIED sqlstate=% message=%', name, SQLSTATE, SQLERRM;
        END IF;
    END;
END $p$;

SELECT pg_temp.probe('S09 UPDATE audit_entries', 'UPDATE public.audit_entries SET outcome = ''Denied''');
SELECT pg_temp.probe('S09 DELETE audit_entries', 'DELETE FROM public.audit_entries');
SELECT pg_temp.probe('S10 TRUNCATE audit_entries', 'TRUNCATE TABLE public.audit_entries');
SELECT pg_temp.probe('S10 TRUNCATE organizations CASCADE', 'TRUNCATE TABLE public.organizations CASCADE');
SELECT pg_temp.probe('S09 INSERT audit_entries direct', 'INSERT INTO public.audit_entries(event_type,outcome,actor_kind,resource_type,correlation_id) VALUES (''x.y'',''Succeeded'',''System'',''x'',''c'')');
SELECT pg_temp.probe('S11 CREATE TABLE public', 'CREATE TABLE public.g16_probe_tbl(a int)');
SELECT pg_temp.probe('S11 CREATE FUNCTION public', 'CREATE FUNCTION public.g16_probe_fn() RETURNS int LANGUAGE sql AS ''SELECT 1''');
SELECT pg_temp.probe('S11 DROP TABLE audit_entries', 'DROP TABLE public.audit_entries');
SELECT pg_temp.probe('S11 ALTER audit_entries DISABLE TRIGGER ALL', 'ALTER TABLE public.audit_entries DISABLE TRIGGER ALL');
SELECT pg_temp.probe('S11 DROP guard trigger no_change', 'DROP TRIGGER m16_audit_no_change ON public.audit_entries');
SELECT pg_temp.probe('S11 DROP guard trigger no_truncate', 'DROP TRIGGER m16_audit_no_truncate ON public.audit_entries');
SELECT pg_temp.probe('S11 REPLACE guard function', 'CREATE OR REPLACE FUNCTION public.m16_audit_guard() RETURNS trigger LANGUAGE plpgsql AS $f$ BEGIN RETURN NEW; END $f$');
SELECT pg_temp.probe('S11 REPLACE capture function', 'CREATE OR REPLACE FUNCTION public.m16_audit_capture() RETURNS trigger LANGUAGE plpgsql AS $f$ BEGIN RETURN NEW; END $f$');
SELECT pg_temp.probe('S11 DROP capture trigger on organizations', 'DROP TRIGGER m16_organizations_audit ON public.organizations');
SELECT pg_temp.probe('S11 ALTER capture trigger disable on shipments', 'ALTER TABLE public.shipments DISABLE TRIGGER m16_shipments_audit');
SELECT pg_temp.probe('S11 CREATE TRIGGER on audit_entries', 'CREATE TRIGGER g16_x AFTER INSERT ON public.audit_entries FOR EACH ROW EXECUTE FUNCTION public.m16_audit_guard()');
SELECT pg_temp.probe('S11 SET session_replication_role replica', 'SET LOCAL session_replication_role = replica');
SELECT pg_temp.probe('S11 ALTER OWNER audit_entries', 'ALTER TABLE public.audit_entries OWNER TO CURRENT_USER');
SELECT pg_temp.probe('S11 GRANT ALL audit_entries to PUBLIC', 'GRANT ALL ON public.audit_entries TO PUBLIC');
SELECT pg_temp.probe('S11 CREATE ROLE', 'CREATE ROLE g16_probe_role');
SELECT pg_temp.probe('S11 CREATE SCHEMA', 'CREATE SCHEMA g16_probe_schema');
SELECT pg_temp.probe('S11 DROP FUNCTION m16_record_audit_access', 'DROP FUNCTION public.m16_record_audit_access(uuid,varchar,uuid,uuid,uuid,varchar)');
SELECT pg_temp.probe('S11 direct call capture function', 'SELECT public.m16_audit_capture()');
SELECT pg_temp.probe('S11 CREATE TEMP TABLE (control)', 'CREATE TEMP TABLE g16_probe_tmp(a int)');
SELECT pg_temp.probe('S12 UPDATE organizations (control, API only)', 'UPDATE public.organizations SET display_name = display_name WHERE false');
SELECT pg_temp.probe('S12 DELETE users', 'DELETE FROM public.users WHERE false');
SELECT pg_temp.probe('S12 DELETE auth_sessions', 'DELETE FROM public.auth_sessions WHERE false');
SELECT pg_temp.probe('S12 DELETE shipments', 'DELETE FROM public.shipments WHERE false');
SELECT pg_temp.probe('S12 UPDATE tracking_inbox_receipts', 'UPDATE public.tracking_inbox_receipts SET id = id WHERE false');
SELECT pg_temp.probe('S12 DELETE tracking_accepted_events', 'DELETE FROM public.tracking_accepted_events WHERE false');
SELECT pg_temp.probe('S12 SELECT user_credentials', 'SELECT count(*) FROM public.user_credentials');

SELECT 'TABLE_PRIV ' || tablename || ' ' || string_agg(priv, ',' ORDER BY priv) AS table_privs
  FROM (
    SELECT c.relname AS tablename, p AS priv
      FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
      CROSS JOIN unnest(ARRAY['SELECT','INSERT','UPDATE','DELETE','TRUNCATE','REFERENCES','TRIGGER']) AS p
     WHERE n.nspname = 'public' AND c.relkind = 'r'
       AND has_table_privilege(current_user, c.oid, p)
  ) t GROUP BY tablename ORDER BY tablename;

SELECT 'SCHEMA_PRIV public CREATE=' || has_schema_privilege(current_user, 'public', 'CREATE')
    || ' USAGE=' || has_schema_privilege(current_user, 'public', 'USAGE')
    || ' DB_CREATE=' || has_database_privilege(current_user, current_database(), 'CREATE')
    || ' DB_TEMP=' || has_database_privilege(current_user, current_database(), 'TEMPORARY');
SELECT 'FUNC_EXEC ' || p.proname || ' ' || has_function_privilege(current_user, p.oid, 'EXECUTE')
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public' AND (p.proname LIKE 'm16\_%' OR p.proname LIKE 'm9\_%' OR p.proname LIKE 'm8\_%')
 ORDER BY p.proname;
