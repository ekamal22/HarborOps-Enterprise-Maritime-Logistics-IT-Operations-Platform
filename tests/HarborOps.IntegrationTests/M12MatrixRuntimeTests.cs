using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Api.Search;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace HarborOps.IntegrationTests;

// G12 filter, input, cursor and tenant-existence matrices through the restricted runtime login.
// Requires an exclusive disposable PostgreSQL server (it provisions the restricted roles).
[Collection("M12ServerRoleIsolation")]
public sealed class M12MatrixRuntimeTests(ITestOutputHelper output)
{
    private void Note(string line)
    {
        output.WriteLine(line);
        var directory = Environment.GetEnvironmentVariable("HARBOROPS_G12_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "matrix-notes.log"),
            $"{DateTimeOffset.UtcNow:O} {line}{Environment.NewLine}");
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Get(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        JsonElement body = default;
        if (text.StartsWith('{')) body = JsonDocument.Parse(text).RootElement.Clone();
        return (response.StatusCode, body);
    }

    private static HashSet<Guid> Ids(JsonElement body) => body.GetProperty("shipments").EnumerateArray()
        .Select(x => x.GetProperty("id").GetGuid()).ToHashSet();

    private static async Task<Guid> Row(M12ResidualBoundaryTests.Ctx c, Guid org, string code, string? external = null,
        string? display = null, string workflow = "Draft", string record = "Active", Guid? origin = null,
        Guid? destination = null, DateTimeOffset? departure = null, DateTimeOffset? arrival = null)
    {
        var id = Guid.NewGuid();
        // The creation guard only admits new Draft rows; fixtures in other states bypass it as the plan seeding does.
        await M12ResidualBoundaryTests.Exec(c.Admin, """
            SET session_replication_role = replica;
            INSERT INTO shipments (id, organization_id, reference_code, external_reference, display_name,
                origin_port_id, destination_port_id, planned_departure_at_utc, planned_arrival_at_utc,
                workflow_status, record_status, source_kind, version, created_at_utc, updated_at_utc,
                archived_at_utc, changed_by_user_id)
            SELECT @id, @org, @code, @ext, @disp,
                COALESCE(@origin, t.origin_port_id), COALESCE(@dest, t.destination_port_id), @dep, @arr,
                @wf, @rec, 'Synthetic', 1, now(), now(), CASE WHEN @rec = 'Archived' THEN now() END,
                t.changed_by_user_id
            FROM shipments t WHERE t.id = @template
            """, ("id", id), ("org", org), ("code", code), ("ext", (object?)external ?? DBNull.Value),
            ("disp", (object?)display ?? DBNull.Value), ("origin", (object?)origin ?? DBNull.Value),
            ("dest", (object?)destination ?? DBNull.Value), ("dep", (object?)departure ?? DBNull.Value),
            ("arr", (object?)arrival ?? DBNull.Value), ("wf", workflow), ("rec", record), ("template", c.Template));
        return id;
    }

    [Fact]
    public async Task TextStatusPortVoyageWindowAndInputMatrixIsExactAndTenantScoped()
    {
        var c = await M12ResidualBoundaryTests.Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var tag = M12ResidualBoundaryTests.Tag();
        var org = c.Fixture.A;
        string Search(string qs) => c.Search + "?" + qs;

        // ---- S03 text, status, null handling, literal wildcard characters
        var alpha = await Row(c, org, tag + "-ALPHA");
        var byExternal = await Row(c, org, tag + "-R2", external: "EXT/" + tag + "/FINDEXT");
        var byDisplay = await Row(c, org, tag + "-R3", display: "Find Mixed Case Display");
        var archived = await Row(c, org, tag + "-ARC", display: "arc row", record: "Archived");
        var booked = await Row(c, org, tag + "-BKD", workflow: "Booked");
        var percent = await Row(c, org, tag + "-PCT", display: "100%_done");
        var lookalike = await Row(c, org, tag + "-PCX", display: "100xxdone");
        var low = tag.ToLowerInvariant();

        async Task<HashSet<Guid>> Ok(string qs)
        {
            var (status, body) = await Get(analyst, Search(qs));
            Assert.True(status == HttpStatusCode.OK, $"{qs}: {status}");
            return Ids(body);
        }
        Assert.Equal(new[] { alpha, byExternal, byDisplay, booked, percent, lookalike }.ToHashSet(), await Ok("q=" + tag + "&limit=100"));
        Assert.Equal(new[] { archived }.ToHashSet(), await Ok("recordStatus=Archived&q=" + tag));
        Assert.Equal(7, (await Ok("recordStatus=All&limit=100&q=" + tag)).Count);
        Assert.Equal(new[] { alpha }.ToHashSet(), await Ok("q=" + low + "-alpha"));
        Assert.Equal(new[] { alpha }.ToHashSet(), await Ok("q=" + Uri.EscapeDataString("  " + tag + "-aLpHa  ")));
        Assert.Equal(new[] { byExternal }.ToHashSet(), await Ok("q=findext"));
        Assert.Equal(new[] { byDisplay }.ToHashSet(), await Ok("q=" + Uri.EscapeDataString("find MIXED case")));
        Assert.Equal(new[] { booked }.ToHashSet(), await Ok("workflowStatus=Booked&q=" + tag));
        Assert.Equal(new[] { percent }.ToHashSet(), await Ok("q=" + Uri.EscapeDataString("100%_")));
        Assert.Equal(new[] { percent }.ToHashSet(), await Ok("q=" + Uri.EscapeDataString("%")));
        foreach (var bad in new[] { "workflowStatus=Nope", "recordStatus=Bogus" })
            Assert.Equal(HttpStatusCode.BadRequest, (await Get(analyst, Search(bad))).Status);

        // ---- S07 input guards
        var guards = new Dictionary<string, HttpStatusCode>
        {
            ["q=" + new string('x', 80)] = HttpStatusCode.OK,
            ["q=" + new string('x', 81)] = HttpStatusCode.BadRequest,
            ["q=%00"] = HttpStatusCode.BadRequest, ["q=%0A"] = HttpStatusCode.BadRequest,
            ["q=" + new string('%', 5000)] = HttpStatusCode.BadRequest,
            ["q=a&q=b"] = HttpStatusCode.BadRequest, ["unknown=1"] = HttpStatusCode.BadRequest,
            ["limit=1"] = HttpStatusCode.OK, ["limit=100"] = HttpStatusCode.OK,
            ["limit=0"] = HttpStatusCode.BadRequest, ["limit=101"] = HttpStatusCode.BadRequest,
            ["limit=-1"] = HttpStatusCode.BadRequest, ["limit=abc"] = HttpStatusCode.BadRequest,
            ["cursor="] = HttpStatusCode.BadRequest, ["voyageId=" + Guid.Empty] = HttpStatusCode.BadRequest,
            ["originPortId=nope"] = HttpStatusCode.BadRequest,
        };
        foreach (var (qs, expected) in guards)
        {
            var (status, _) = await Get(analyst, Search(qs));
            Assert.True(status == expected, $"{(qs.Length > 60 ? qs[..60] + "..." : qs)}: expected {expected} got {status}");
        }
        using (var exportCursor = await manager.GetAsync(c.Export + "?limit=5"))
            Assert.Equal(HttpStatusCode.BadRequest, exportCursor.StatusCode);

        // ---- S06 UTC windows are half-open and the only accepted forms are explicit UTC instants
        var start = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero);
        var micro = TimeSpan.FromTicks(10);
        var wtag = M12ResidualBoundaryTests.Tag();
        var onStart = await Row(c, org, wtag + "-W1", departure: start, arrival: start.AddDays(1));
        var onEnd = await Row(c, org, wtag + "-W2", departure: end, arrival: end.AddDays(1));
        var justInside = await Row(c, org, wtag + "-W3", departure: end - micro, arrival: end.AddDays(1) - micro);
        var justBefore = await Row(c, org, wtag + "-W4", departure: start - micro, arrival: start.AddDays(1) - micro);
        const string from = "2026-03-01T00%3A00%3A00Z", before = "2026-03-31T00%3A00%3A00Z";
        Assert.Equal(new[] { onStart, justInside }.ToHashSet(),
            await Ok($"q={wtag}&plannedDepartureFromUtc={from}&plannedDepartureBeforeUtc={before}"));
        Assert.Equal(new[] { onStart, justInside }.ToHashSet(),
            await Ok($"q={wtag}&plannedArrivalFromUtc=2026-03-02T00%3A00%3A00Z&plannedArrivalBeforeUtc=2026-04-01T00%3A00%3A00Z"));
        Assert.Equal(new[] { onStart, justInside }.ToHashSet(), await Ok(
            $"q={wtag}&plannedDepartureFromUtc=2026-03-01T00%3A00%3A00%2B00%3A00&plannedDepartureBeforeUtc=2026-03-31T00%3A00%3A00.000000Z"));
        foreach (var window in new[]
        {
            "plannedDepartureFromUtc=2026-03-01T01%3A00%3A00%2B01%3A00&plannedDepartureBeforeUtc=" + before,
            "plannedDepartureFromUtc=2026-03-01&plannedDepartureBeforeUtc=2026-03-31",
            "plannedDepartureFromUtc=2026-03-01T00%3A00%3A00&plannedDepartureBeforeUtc=" + before,
            "plannedDepartureFromUtc=" + from + "&plannedDepartureBeforeUtc=" + from,
            "plannedDepartureFromUtc=" + before + "&plannedDepartureBeforeUtc=" + from,
            "plannedDepartureFromUtc=" + from,
            "plannedDepartureFromUtc=2025-01-01T00%3A00%3A00Z&plannedDepartureBeforeUtc=2026-03-01T00%3A00%3A00Z",
        })
        {
            var (status, _) = await Get(analyst, Search("q=" + wtag + "&" + window));
            Assert.True(status == HttpStatusCode.BadRequest, $"window {window}: expected 400 got {status}");
        }

        // ---- S05 port filters: independent, combined, and never crossing tenants
        var portC = Guid.NewGuid(); var portD = Guid.NewGuid(); var foreignPort = Guid.NewGuid();
        foreach (var (id, owner, code) in new[] { (portC, org, "MXP-C"), (portD, org, "MXP-D"), (foreignPort, c.Fixture.B, "MXP-F") })
            await M12ResidualBoundaryTests.Exec(c.Admin, """
                INSERT INTO ports (id, organization_id, reference_code, name, country_code, un_locode, status, source_kind,
                    version, created_at_utc, updated_at_utc)
                VALUES (@id, @org, @code, @code, 'US', NULL, 'Active', 'Synthetic', 1, now(), now())
                """, ("id", id), ("org", owner), ("code", code + "-" + wtag));
        var cd = await Row(c, org, wtag + "-P1", origin: portC, destination: portD);
        var cOnly = await Row(c, org, wtag + "-P2", origin: portC);
        Assert.Equal(new[] { cd, cOnly }.ToHashSet(), await Ok($"q={wtag}-P&originPortId={portC}"));
        Assert.Equal(new[] { cd }.ToHashSet(), await Ok($"q={wtag}-P&destinationPortId={portD}"));
        Assert.Equal(new[] { cd }.ToHashSet(), await Ok($"q={wtag}-P&originPortId={portC}&destinationPortId={portD}"));
        Assert.Empty(await Ok($"q={wtag}-P&originPortId={portD}&destinationPortId={portC}"));
        Assert.Empty(await Ok($"originPortId={foreignPort}"));
        Assert.Empty(await Ok($"originPortId={Guid.NewGuid()}"));

        // ---- S04 voyage assignments: open only, not closed, not future, latest assignment wins
        var voyage1 = await M12ResidualBoundaryTests.Scalar<Guid>(c.Admin,
            "SELECT id FROM voyages WHERE organization_id=@o ORDER BY created_at_utc LIMIT 1", ("o", org));
        var voyage2 = Guid.NewGuid();
        await M12ResidualBoundaryTests.Exec(c.Admin, """
            INSERT INTO voyages (id, organization_id, reference_code, vessel_id, origin_port_id, destination_port_id,
                planned_departure_at_utc, planned_arrival_at_utc, actual_departure_at_utc, actual_arrival_at_utc,
                voyage_status, record_status, source_kind, version, created_at_utc, updated_at_utc)
            SELECT @id, organization_id, @code, vessel_id, origin_port_id, destination_port_id, NULL, NULL, NULL, NULL,
                'Planned', 'Active', 'Synthetic', 1, now(), now() FROM voyages WHERE id=@v1
            """, ("id", voyage2), ("code", "MXV-" + wtag), ("v1", voyage1));
        var open1 = await Row(c, org, wtag + "-V1"); var closed1 = await Row(c, org, wtag + "-V2");
        var future1 = await Row(c, org, wtag + "-V3"); var moved = await Row(c, org, wtag + "-V4");
        var now = DateTimeOffset.UtcNow;
        async Task Assign(Guid shipment, Guid voyage, DateTimeOffset starts, DateTimeOffset? ends) =>
            await M12ResidualBoundaryTests.Exec(c.Admin, """
                SET session_replication_role = replica;
                INSERT INTO shipment_voyage_assignments (id, organization_id, shipment_id, voyage_id, starts_at_utc,
                    ends_at_utc, version, created_at_utc, updated_at_utc, created_by_user_id, closed_by_user_id)
                VALUES (gen_random_uuid(), @org, @s, @v, @st, @en::timestamptz, 1, now(), now(), @u, CASE WHEN @en::timestamptz IS NULL THEN NULL ELSE @u::uuid END)
                """, ("org", org), ("s", shipment), ("v", voyage), ("st", starts),
                ("en", (object?)ends ?? DBNull.Value), ("u", c.ManagerId));
        await Assign(open1, voyage1, now.AddDays(-3), null);
        await Assign(closed1, voyage1, now.AddDays(-8), now.AddDays(-2));
        await Assign(future1, voyage1, now.AddDays(1), null);
        await Assign(moved, voyage1, now.AddDays(-10), now.AddDays(-5));
        await Assign(moved, voyage2, now.AddDays(-5), null);
        Assert.Equal(new[] { open1 }.ToHashSet(), await Ok($"q={wtag}-V&voyageId={voyage1}"));
        Assert.Equal(new[] { moved }.ToHashSet(), await Ok($"q={wtag}-V&voyageId={voyage2}"));
        Assert.Empty(await Ok($"voyageId={Guid.NewGuid()}"));
        Note("S03-S07 matrix: text, status, wildcard literals, input guards, half-open windows, ports and voyage variants asserted");
    }

    [Fact]
    public async Task CursorIsBoundToEveryFieldAndRefusesTamperExpiryAndForeignSessions()
    {
        var c = await M12ResidualBoundaryTests.Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var tag = M12ResidualBoundaryTests.Tag();
        await M12ResidualBoundaryTests.InsertShipments(c, c.Fixture.A, tag, 12, "'Cursor ' || n", "NULL", "@tag || '-' || lpad(n::text, 3, '0')");
        var (status, page) = await Get(analyst, c.Search + "?limit=5&q=" + tag);
        Assert.Equal(HttpStatusCode.OK, status);
        var cursor = page.GetProperty("nextCursor").GetString()!;
        string Next(string token, string qs = "limit=5&q=") => c.Search + "?" + qs + tag + "&cursor=" + Uri.EscapeDataString(token);
        Assert.Equal(HttpStatusCode.OK, (await Get(analyst, Next(cursor))).Status);

        // Tamper at the start, middle and end; truncation; garbage; wrong limit; wrong filter.
        var chars = cursor.ToCharArray();
        foreach (var index in new[] { 0, cursor.Length / 2, cursor.Length - 1 })
        {
            var copy = (char[])chars.Clone();
            copy[index] = copy[index] == 'A' ? 'B' : 'A';
            Assert.Equal(HttpStatusCode.Conflict, (await Get(analyst, Next(new string(copy)))).Status);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Get(analyst, Next(cursor[..^6]))).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Get(analyst, Next("not-a-cursor-value"))).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Get(analyst, c.Search + "?limit=4&q=" + tag + "&cursor=" + Uri.EscapeDataString(cursor))).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Get(analyst, c.Search + "?limit=5&workflowStatus=Draft&q=" + tag + "&cursor=" + Uri.EscapeDataString(cursor))).Status);

        // A different session of the same user, and a different user, cannot replay it.
        using var secondSession = await M12ResidualBoundaryTests.Client(c.Runtime, c.Fixture.AnalystEmail, c.Fixture.A);
        Assert.Equal(HttpStatusCode.Conflict, (await Get(secondSession, Next(cursor))).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Get(manager, Next(cursor))).Status);

        // Expiry and binding are enforced by the codec with an injected clock.
        var codec = new ShipmentCursorCodec(DataProtectionProvider.Create(
            new DirectoryInfo(Path.Combine(Path.GetTempPath(), "m12-cursor-" + Guid.NewGuid().ToString("N")))));
        var org = Guid.NewGuid(); var actor = Guid.NewGuid(); var session = Guid.NewGuid();
        var issued = DateTimeOffset.UtcNow; var after = Guid.NewGuid();
        var token = codec.Encode(org, actor, session, "FP", after, issued, issued);
        Assert.True(codec.TryDecode(token, org, actor, session, "FP", issued.AddMinutes(14), out var parsed));
        Assert.Equal(after, parsed!.After);
        Assert.False(codec.TryDecode(token, org, actor, session, "FP", issued.AddMinutes(15).AddSeconds(1), out _));
        Assert.False(codec.TryDecode(token, org, actor, session, "FP", issued.AddMinutes(16), out _));
        Assert.False(codec.TryDecode(token, Guid.NewGuid(), actor, session, "FP", issued, out _));
        Assert.False(codec.TryDecode(token, org, Guid.NewGuid(), session, "FP", issued, out _));
        Assert.False(codec.TryDecode(token, org, actor, Guid.NewGuid(), "FP", issued, out _));
        Assert.False(codec.TryDecode(token, org, actor, session, "OTHER", issued, out _));
        var futureAsOf = codec.Encode(org, actor, session, "FP", after, issued.AddMinutes(5), issued);
        Assert.False(codec.TryDecode(futureAsOf, org, actor, session, "FP", issued, out _));
        var emptyAfter = codec.Encode(org, actor, session, "FP", Guid.Empty, issued, issued);
        Assert.False(codec.TryDecode(emptyAfter, org, actor, session, "FP", issued, out _));
        Note("S08 cursor matrix: tamper, truncation, limit, filter, session, actor, expiry, future as-of and empty cursor all refused");
    }

    [Fact]
    public async Task ForeignTenantExistenceIsIndistinguishableAndDetailFollowsLifecycleAndAccess()
    {
        var c = await M12ResidualBoundaryTests.Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var tag = M12ResidualBoundaryTests.Tag();
        var orgB = c.Fixture.B;
        var portB1 = Guid.NewGuid(); var portB2 = Guid.NewGuid(); var shipmentB = Guid.NewGuid();
        foreach (var (id, code) in new[] { (portB1, "XB-1"), (portB2, "XB-2") })
            await M12ResidualBoundaryTests.Exec(c.Admin, """
                INSERT INTO ports (id, organization_id, reference_code, name, country_code, un_locode, status, source_kind,
                    version, created_at_utc, updated_at_utc)
                VALUES (@id, @org, @code, @code, 'US', NULL, 'Active', 'Synthetic', 1, now(), now())
                """, ("id", id), ("org", orgB), ("code", code + "-" + tag));
        await M12ResidualBoundaryTests.Exec(c.Admin, """
            INSERT INTO shipments (id, organization_id, reference_code, origin_port_id, destination_port_id, workflow_status,
                record_status, source_kind, version, created_at_utc, updated_at_utc, changed_by_user_id)
            SELECT @id, @org, @code, @o, @d, 'Draft', 'Active', 'Synthetic', 1, now(), now(), changed_by_user_id
            FROM shipments WHERE id=@t
            """, ("id", shipmentB), ("org", orgB), ("code", tag + "-FOREIGN"), ("o", portB1), ("d", portB2), ("t", c.Template));

        async Task<(HttpStatusCode, string?, string?)> Probe(string url)
        {
            var (status, body) = await Get(manager, url);
            return (status, body.ValueKind == JsonValueKind.Object && body.TryGetProperty("code", out var code) ? code.GetString() : null,
                body.ValueKind == JsonValueKind.Object && body.TryGetProperty("message", out var message) ? message.GetString() : null);
        }
        string Detail(Guid org, Guid id) => $"/api/v1/organizations/{org}/shipments/{id}";
        // Foreign shipment id in my organization route versus an id that exists nowhere.
        var foreignInMine = await Probe(Detail(c.Fixture.A, shipmentB));
        var nowhereInMine = await Probe(Detail(c.Fixture.A, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, foreignInMine.Item1);
        Assert.Equal(nowhereInMine, foreignInMine);
        // Foreign organization route versus an organization that exists nowhere.
        var foreignOrg = await Probe($"/api/v1/organizations/{orgB}/shipments/search");
        var nowhereOrg = await Probe($"/api/v1/organizations/{Guid.NewGuid()}/shipments/search");
        Assert.Equal(HttpStatusCode.NotFound, foreignOrg.Item1);
        Assert.Equal(nowhereOrg, foreignOrg);
        var foreignExport = await Probe($"/api/v1/organizations/{orgB}/exports/shipments");
        Assert.Equal(nowhereOrg, foreignExport);
        // Foreign filter identifiers and a foreign text token never reveal foreign rows.
        var (s1, body1) = await Get(analyst, c.Search + "?q=" + tag + "-FOREIGN");
        Assert.Equal(HttpStatusCode.OK, s1); Assert.Empty(Ids(body1));
        var (s2, body2) = await Get(analyst, c.Search + "?originPortId=" + portB1);
        Assert.Equal(HttpStatusCode.OK, s2); Assert.Empty(Ids(body2));

        // Detail follows the lifecycle: archived rows stay readable, restore returns them to Active.
        var mine = await M12ResidualBoundaryTests.Scalar<Guid>(c.Admin,
            "SELECT id FROM shipments WHERE id=@t", ("t", c.Template));
        var version = await M12ResidualBoundaryTests.Scalar<long>(c.Admin, "SELECT version FROM shipments WHERE id=@t", ("t", mine));
        using (var archive = await ReferenceDataHttpTests.Send(c.Fixture.Manager, HttpMethod.Delete, Detail(c.Fixture.A, mine),
            new { expectedVersion = version }))
            Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        var (ds, db) = await Get(analyst, Detail(c.Fixture.A, mine));
        Assert.Equal(HttpStatusCode.OK, ds);
        Assert.Equal("Archived", db.GetProperty("recordStatus").GetString());
        var (_, archivedSearch) = await Get(analyst, c.Search + "?recordStatus=Archived&limit=100");
        Assert.Contains(mine, Ids(archivedSearch));
        var (_, activeSearch) = await Get(analyst, c.Search + "?limit=100");
        Assert.DoesNotContain(mine, Ids(activeSearch));
        using (var restore = await ReferenceDataHttpTests.Send(c.Fixture.Manager, HttpMethod.Patch, Detail(c.Fixture.A, mine),
            new { expectedVersion = version + 1, recordStatus = "Active" }))
            Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Equal("Active", (await Get(analyst, Detail(c.Fixture.A, mine))).Body.GetProperty("recordStatus").GetString());

        // Reauthorization: a removed member loses detail and search immediately.
        var analystId = await M12ResidualBoundaryTests.Scalar<Guid>(c.Admin, "SELECT id FROM users WHERE email=@e", ("e", c.Fixture.AnalystEmail));
        await M12ResidualBoundaryTests.Exec(c.Admin,
            "UPDATE organization_memberships SET status='Removed', version=version+1, updated_at_utc=now() WHERE organization_id=@o AND user_id=@u",
            ("o", c.Fixture.A), ("u", analystId));
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(analyst, Detail(c.Fixture.A, mine))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(analyst, c.Search + "?limit=1")).Status);
        Note("S10/S12: foreign existence probes equal to nonexistent, archived detail and search transitions, reauthorization after removal");
    }
}
