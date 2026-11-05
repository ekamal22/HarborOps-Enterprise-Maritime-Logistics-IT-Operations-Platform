-- Run only against a controlled development database after applying the M9 migration.
-- Set the login password interactively using psql's \password harborops_worker.
-- Never pass the password in SQL, shell arguments, source files or logs.
DO $role$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='harborops_worker') THEN
        CREATE ROLE harborops_worker LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
    END IF;
END $role$;
GRANT CONNECT ON DATABASE harborops TO harborops_worker;
GRANT USAGE ON SCHEMA public TO harborops_worker;
GRANT SELECT ON public.organizations, public.tracking_inbox_work_items,
    public.tracking_inbox_receipts, public.tracking_accepted_events,
    public.tracking_work_attempt_events, public.tracking_processing_effects,
    public.containers, public.shipments,
    public.shipment_container_assignments TO harborops_worker;
GRANT UPDATE (state,attempt_count,lease_token,lease_epoch,lease_expires_at_utc,
    available_at_utc,redrive_generation,last_error_code,completed_at_utc,
    resolution_kind,updated_at_utc)
    ON public.tracking_inbox_work_items TO harborops_worker;
GRANT INSERT ON public.tracking_processing_effects TO harborops_worker;
REVOKE INSERT, UPDATE, DELETE ON public.tracking_work_attempt_events FROM harborops_worker;
GRANT EXECUTE ON FUNCTION public.m9_lock_active_organization(uuid)
    TO harborops_worker;
