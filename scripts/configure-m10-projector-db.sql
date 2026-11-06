-- Run manually as the database administrator after the accepted M10 migration.
-- Set the projector role password interactively using: \password harborops_projector
-- Never save that password in a source-controlled file or a command argument.
DO $role$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='harborops_projector') THEN
        CREATE ROLE harborops_projector LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;
END $role$;

GRANT CONNECT ON DATABASE harborops TO harborops_projector;
GRANT USAGE ON SCHEMA public TO harborops_projector;
GRANT SELECT ON TABLE organizations,shipments,tracking_processing_effects,
    tracking_accepted_events,tracking_inbox_receipts,
    shipment_tracking_corrections,shipment_tracking_projections,
    shipment_tracking_input_versions,shipment_tracking_correction_heads,
    shipment_tracking_timeline_entries,
    shipment_projection_applications TO harborops_projector;
GRANT INSERT,UPDATE ON TABLE shipment_tracking_projections TO harborops_projector;
GRANT INSERT ON TABLE shipment_projection_applications TO harborops_projector;
-- No update or delete access to M8 receipts, accepted events or M9 effects/work.

GRANT EXECUTE ON FUNCTION m10_lock_projector_scope(uuid,uuid) TO harborops_projector;
