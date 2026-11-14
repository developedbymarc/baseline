# BASELINE backend

ASP.NET Core (.NET 10) + EF Core + SQLite. One backend serves the `/crew` and `/ground` UIs.
**All data is synthetic. BASELINE is decision support, not diagnosis.**

The API contract for the frontend is in [../docs/API.md](../docs/API.md).

## Run (offline, one command)

```bash
cd backend
dotnet run              # creates baseline.db and seeds the demo mission if it is empty
dotnet run -- reseed    # wipe baseline.db and reload the demo mission
```

- API: `http://localhost:5074` (CORS allows `http://localhost:5173` and `:4173`)
- API docs UI: `http://localhost:5074/scalar` (Development only)
- Requires the .NET 10 SDK. No internet needed at runtime.

## Demo mission (seed, WP 3.5)

`Services/Seed/DemoSeeder.cs`: a Mars-transit mission, 12 min one-way latency, 4 crew, 14 pre-flight days and 90 in-flight days, all labelled SYNTHETIC. Baselines are locked at mission start. The mission clock starts 5 min after launch, so the 90 in-flight days lie ahead of the clock and appear as the presenter fast-forwards (endpoints hide anything after mission time now).

| Crew member | Planted event |
|---|---|
| Alex Rivera | Slow BP drift from mission day 8, levelling off at day 23 (about +1.8 SD) |
| Sam Okafor | Sleep decline from day 30 (about 4.8 h on days 36-45) with a mood dip; cabin CO2 3.6 mmHg on day 38 |
| Jordan Lee | One-day fever on day 52 (38.6 °C) with a heart-rate rise |
| Rana Haddad | Nothing logged on days 60-64 (missed-measurement streak) |
| Everyone | Geomagnetic storm (Kp 6) and a solar particle event on day 25 |

Vital signs are drawn around the middle of each metric's normal band from `config/metrics.yaml`. In-flight noise is 0.7 of the pre-flight spread so that ordinary noise stays mostly green. Alerts and context factors for the seeded readings are precomputed with the real engines and reach ground through the link with the mission latency. Daily reminders are seeded on the crew side.

Demo script tip: fast-forward (`POST /api/demo/clock {"fastForward": true}`), then switch back with `{"fastForward": false}` before step 5, or the 30-minute chest pain window and the 12-minute delay are over almost at once.

## Test

```bash
dotnet test ../baseline.slnx
```

Tests live in `../backend.Tests` (372 tests):

| File | What it covers |
|---|---|
| `BaselineEngineTests`, `StatusEngineTests`, `RealConfigEdgeTests` | Mean, SD, shrunk SD; threshold and z bands, polarity, worse-of rule, values exactly on a limit (real limits) |
| `ContextEngineTests`, `ContextRulesConfigTests` | Every context rule fires or stays quiet, window edges, downgrade policy; strict YAML validation |
| `MissionClockTests`, `LinkQueueTests`, `ReminderTests` | Clock rate / pause / fast-forward; latency queue, held messages, original timestamps; reminders |
| `ReportEngineTests`, `ReportApiTests` | Hand-computed Theil-Sen, Mann-Kendall, z forms, missed counts, highlights; reports over HTTP; 4 crew x 90 days in < 3 s |
| `PacketTests` | SBAR packet content for a red BP and a chest pain run; one readable object |
| `RolesTests` | Mock roles: crew own data only; ground reads only delivered data |
| `DemoFlowTests` | Demo script steps 2-5 end to end |
| `SeedTests` | Seed is deterministic, labelled synthetic, and the planted events give the expected statuses, trends and factors |
| `HealthConfigTests`, `ConfigSchemaTests`, `ConfigCrossFileTests` | YAML loading, schema, cross-file references, how-to files, version line |
| `ApiTests`, `ContextAndTriggerApiTests`, `DbContextTests` | End-to-end API flows; chest pain trigger window; baseline lock; SQLite date queries |

## Layout

- `Configuration/HealthConfig.cs`: loads and validates `config/metrics.yaml`, `config/context_rules.yaml` (strict: unknown keys are errors) and every protocol YAML (any file with `tree_yaml`) at start. Broken references stop startup; content warnings (unopened sources, `review: true`, missing `sd_pop`, missing how-to files) are logged and never stop it. Startup logs one line with the version of each file.
- `Auth/Caller.cs`: mock roles from the `X-Role` / `X-Crew-Id` headers.
- `Services/Engines`: `BaselineEngine` (WP 4.1), `StatusEngine` (4.2), `ContextEngine` (4.3). Pure functions.
- `Services/Reports`: biweekly and monthly reports (4.4): `TrendStats` (Theil-Sen, Mann-Kendall), `ReportEngine` (pure), `ReportService`.
- `Services/Packets` + `Templates/sbar_packet.sbn`: SBAR handoff packet builder (4.5).
- `Services/Clock/MissionClock.cs`: the mission clock (3.3). Everything time-based uses mission time.
- `Services/Link/LinkQueue.cs`: the latency simulator rules (3.4).
- `Services/Reminders`: reminder schedules and completion (crew side, never sent through the link).
- `Services/Missions/MissionService.cs`: DB-backed orchestration (measurements, alerts, symptoms, protocol runs, link sync).
- `Services/Seed/DemoSeeder.cs`: the demo mission (3.5).

## Rules the code follows

- Status = worse of the absolute status and the z-score status. Limits come from `metrics.yaml`; values exactly on a limit count as inside the band. Polarity `low` flags only falling values, `high` only rising, `both` either; `none` (context inputs) has no status.
- Shrunk SD `sqrt((n·sd² + k·sd_pop²)/(n+k))` with k = 10 when n < 10 and the metric has `sd_pop`; otherwise the personal SD (with a warning).
- Baselines are locked at mission start; `ApplicationDbContext` throws if a locked baseline changes. No audit table.
- Link: sent while up → `deliver_at = sent_at + latency`; sent while down → held, then reconnect time + latency; in-flight messages still arrive; `sent_at` never changes. In-flight measurements, symptoms and protocol events are synced to ground as link messages; ground reads only delivered data.
- Context rules run for yellow / red in-flight readings; factors are stored as JSON on the alert. At most one level of downgrade, only from yellow; temp and SpO2 are never downgraded; the original flag is always kept. `metric_count` counts distinct mission days using each day's latest reading.
- Chest pain: a red reading on `trigger.red_metrics` offers the protocol only with a `trigger.symptoms` symptom within `window_min` mission minutes (either order); the symptom alone always offers it.
- Reminders and reports use one rule for completion: each reading completes the latest open slot whose window holds it (due − 2 h to the next due time); a slot still open at the next due time is missed.
- Report trends: rising / falling only if Mann-Kendall p < 0.05 and the Theil-Sen change over the window is at least 0.5 × sd_eff; low confidence below 5 readings (`appsettings.json` → `Reports`).
