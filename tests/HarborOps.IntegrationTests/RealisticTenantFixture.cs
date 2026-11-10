using System.Diagnostics;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// A tenant that looks like production: skewed status mix, archived rows, planned
// dates, trigger-written status history, fresh rule evaluations for in-flight
// shipments and open exceptions. Roughly five report members per shipment.
internal static class RealisticTenantFixture
{
    internal static async Task<(long History, long Evaluations, long OpenExceptions, long Milliseconds)>
        SeedAsync(NpgsqlConnection sql, Guid org, Guid origin, Guid destination,
            Guid actor, int shipments)
    {
        var seedClock = Stopwatch.StartNew();
        // The shipment trigger only admits Draft version 1 rows, then legal
        // single-step transitions with version increments, so the skew is
        // built the same way production data is: insert, then transition.
        await using (var bulk = new NpgsqlCommand("""
            INSERT INTO shipments (id,organization_id,reference_code,origin_port_id,
                destination_port_id,planned_departure_at_utc,planned_arrival_at_utc,
                workflow_status,record_status,source_kind,version,created_at_utc,
                updated_at_utc,changed_by_user_id)
            SELECT gen_random_uuid(),@org,'G13R-' || lpad(n::text,6,'0'),@origin,
                @destination,
                clock_timestamp()-interval '10 days'+(n%5000)*interval '1 minute',
                clock_timestamp()-interval '5 days'+(n%5000)*interval '1 minute',
                'Draft','Active','Synthetic',1,
                clock_timestamp()-interval '30 days',clock_timestamp()-interval '30 days',@actor
            FROM generate_series(1,@count) AS n
            """, sql) { CommandTimeout = 1800 })
        {
            bulk.Parameters.AddWithValue("org", org);
            bulk.Parameters.AddWithValue("origin", origin);
            bulk.Parameters.AddWithValue("destination", destination);
            bulk.Parameters.AddWithValue("actor", actor);
            bulk.Parameters.AddWithValue("count", shipments);
            Assert.Equal(shipments, await bulk.ExecuteNonQueryAsync());
        }
        // Rank by the numeric suffix: 1 Draft 35%, 2 Booked 20%, 3 InTransit 20%,
        // 4 Arrived 12%, 5 Completed 9%, 6 Cancelled 4%.
        const string Rank = """
            CASE WHEN right(reference_code,6)::int%100<35 THEN 1
                 WHEN right(reference_code,6)::int%100<55 THEN 2
                 WHEN right(reference_code,6)::int%100<75 THEN 3
                 WHEN right(reference_code,6)::int%100<87 THEN 4
                 WHEN right(reference_code,6)::int%100<96 THEN 5 ELSE 6 END
            """;
        var steps = new (string From, string To, string Condition)[]
        {
            ("Draft","Booked",$"({Rank}) BETWEEN 2 AND 5"),
            ("Booked","InTransit",$"({Rank}) BETWEEN 3 AND 5"),
            ("InTransit","Arrived",$"({Rank}) BETWEEN 4 AND 5"),
            ("Arrived","Completed",$"({Rank})=5"),
            ("Draft","Cancelled",$"({Rank})=6")
        };
        foreach (var step in steps)
        {
            await using var move = new NpgsqlCommand($"""
                UPDATE shipments SET workflow_status='{step.To}',version=version+1,
                    updated_at_utc=clock_timestamp(),changed_by_user_id=@actor
                WHERE organization_id=@org AND reference_code LIKE 'G13R-%'
                  AND workflow_status='{step.From}' AND {step.Condition}
                """, sql) { CommandTimeout = 1800 };
            move.Parameters.AddWithValue("org", org);
            move.Parameters.AddWithValue("actor", actor);
            await move.ExecuteNonQueryAsync();
        }
        // About 3 percent are archived afterwards.
        await using (var archive = new NpgsqlCommand("""
            UPDATE shipments SET record_status='Archived',archived_at_utc=clock_timestamp(),
                version=version+1,updated_at_utc=clock_timestamp(),changed_by_user_id=@actor
            WHERE organization_id=@org AND reference_code LIKE 'G13R-%'
              AND right(reference_code,6)::int%37=0
            """, sql) { CommandTimeout = 1800 })
        {
            archive.Parameters.AddWithValue("org", org);
            archive.Parameters.AddWithValue("actor", actor);
            await archive.ExecuteNonQueryAsync();
        }
        // The shipment trigger writes the immutable status history for every
        // legal transition above; direct inserts into it are forbidden.
        long history;
        await using (var changes = new NpgsqlCommand(
            "SELECT count(*) FROM shipment_status_changes WHERE organization_id=@org", sql))
        {
            changes.Parameters.AddWithValue("org", org);
            history = Convert.ToInt64(await changes.ExecuteScalarAsync());
        }
        Assert.True(history >= shipments, "Transitions must have produced status history");
        long evaluations, openExceptions;
        await using (var evaluate = new NpgsqlCommand("""
            INSERT INTO shipment_exception_evaluations (organization_id,shipment_id,
                rule_code,rule_policy_version,condition_state,source_fingerprint,
                shipment_version,tracking_input_revision,correction_generation)
            SELECT s.organization_id,s.id,r.code,'m11-rules-v1',
                CASE WHEN right(s.reference_code,6)::int%20=0 THEN 'Active' ELSE 'Inactive' END,
                repeat('a',64),s.version,0,0
            FROM shipments s
            CROSS JOIN (VALUES ('EquipmentConflict'),('ArrivalScheduleOverdue'),
                ('RecordedArrivalLate'),('DepartureSlippageRisk')) AS r(code)
            WHERE s.organization_id=@org AND s.reference_code LIKE 'G13R-%'
              AND s.record_status='Active'
              AND s.workflow_status IN ('Booked','InTransit')
            """, sql) { CommandTimeout = 1800 })
        {
            evaluate.Parameters.AddWithValue("org", org);
            evaluations = await evaluate.ExecuteNonQueryAsync();
        }
        await using (var exceptions = new NpgsqlCommand("""
            INSERT INTO shipment_exceptions (organization_id,shipment_id,rule_code,
                occurrence,condition_state,lifecycle_state,severity,confidence,
                reason_code,source_fingerprint,shipment_version,tracking_input_revision,
                correction_generation,evidence)
            SELECT e.organization_id,e.shipment_id,e.rule_code,1,'Active','Open',
                'Warning','ScheduleOnly','seed_fixture',repeat('a',64),
                e.shipment_version,0,0,'{}'::jsonb
            FROM shipment_exception_evaluations e
            WHERE e.organization_id=@org AND e.condition_state='Active'
            """, sql) { CommandTimeout = 1800 })
        {
            exceptions.Parameters.AddWithValue("org", org);
            openExceptions = await exceptions.ExecuteNonQueryAsync();
        }
        await using (var statistics = new NpgsqlCommand("""
            ANALYZE shipments; ANALYZE shipment_status_changes;
            ANALYZE shipment_exception_evaluations; ANALYZE shipment_exceptions;
            """, sql) { CommandTimeout = 300 })
            await statistics.ExecuteNonQueryAsync();
        seedClock.Stop();
        return (history, evaluations, openExceptions, seedClock.ElapsedMilliseconds);
    }
}
