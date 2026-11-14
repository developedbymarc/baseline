# BASELINE backend

ASP.NET Core (.NET 10) + EF Core + SQLite. One backend serves the `/crew` and `/ground` UIs.
**All data is synthetic. BASELINE is decision support, not diagnosis.**

## Run (offline, one command)

```bash
cd backend
dotnet run              # creates baseline.db and seeds the demo mission if it is empty
dotnet run -- reseed    # wipe baseline.db and reload the demo mission
```

API docs: http://localhost:5074/scalar (Development only).

## Test

```bash
dotnet test ../baseline.slnx
```

Tests live in `../backend.Tests`:

| File | What it covers |
|---|---|
| `BaselineEngineTests` | mean, sample SD, shrunk SD for n < 10 (hand-computed) |
| `StatusEngineTests` | table-driven threshold bands, z-score bands, polarity, worse-of rule, plausibility / °F check |
| `MissionClockTests` | rate, pause, fast-forward (1 day per 5 s), advance |
| `LinkQueueTests` | deliver_at = sent_at + latency, link down holds messages, reconnect drains them, original timestamps kept |
| `ReminderTests` | schedule parsing, occurrences, done / late / missed |
| `ContextEngineTests` | every context rule fires / stays quiet, window edges, downgrade policy |
| `ContextRulesConfigTests` | context_rules.yaml strict validation (table-driven), trigger `window_min` |
| `ContextAndTriggerApiTests` | context factors on real submissions; chest pain trigger window in both orders |
| `ConfigSchemaTests`, `RealConfigEdgeTests` | YAML schema (versions, source fields, sd_pop, outcomes) and band edges on the real limits |
| `HealthConfigTests` | the real YAML in `config/` loads; bad YAML is rejected |
| `DbContextTests` | locked baselines cannot change; DateTimeOffset queries work in SQLite |
| `ApiTests` | end-to-end over HTTP with a controllable clock |

## Layout

- `Configuration/HealthConfig.cs`: loads and validates `config/metrics.yaml`, `config/context_rules.yaml` (strict: unknown keys are errors) and every protocol YAML (any file with `tree_yaml`) at start. The app does not start if the YAML is invalid; content warnings (unopened sources, `review: true`, missing `sd_pop`) are logged and never stop it.
- `Services/Engines`: `BaselineEngine` (WP 4.1), `StatusEngine` (WP 4.2) and `ContextEngine` (WP 4.3). Pure functions with no DB access.
- `Services/Clock/MissionClock.cs`: the mission clock (WP 3.3). Everything time-based uses mission time.
- `Services/Link/LinkQueue.cs`: the latency simulator rules (WP 3.4).
- `Services/Reminders`: reminder schedules (stored on the crew side, never sent through the link).
- `Services/Missions/MissionService.cs`: DB-backed orchestration (start mission, submit a measurement, queue a packet).
- `Services/Seed/DemoSeeder.cs`: minimal synthetic seed. Member D's generator replaces it and adds the planted anomalies.

## API summary

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/mission` | Mission state: mission time, MET, link, latency, `onboardFirst` (E3) |
| POST | `/api/mission/start` | Compute and lock baselines, switch to in-flight (A4) |
| POST | `/api/demo/clock` | `{ fastForward?, rate?, paused?, advanceSeconds? }` (G2) |
| POST | `/api/demo/link` | `{ up?, oneWayLatencySeconds? }` (G3) |
| POST | `/api/demo/reseed` | Reload the demo mission |
| GET | `/api/config/metrics`, `/api/config/protocols/{id}` | YAML content for the UI |
| GET | `/api/crew` | Crew list |
| GET | `/api/crew/{id}/baselines` | mean, sd, sdEffective, shrunk, n, lockedAt |
| GET | `/api/crew/{id}/measurements?metric=&days=` | History for charts, with the status of each point (D1) |
| POST | `/api/crew/{id}/measurements` | `{ metricCode, value, confirmImplausible?, note? }`, returns the status. 422 = implausible, confirm or retake (C2, C3) |
| GET | `/api/crew/{id}/reminders?openOnly=` | Due and overdue reminders (B2, B3) |
| GET | `/api/crew/{id}/alerts` | Alerts for this crew member |
| POST | `/api/crew/{id}/symptoms` | `{ symptomCode }`. A code that matches a protocol id (e.g. `chest_pain`) raises a red alert and returns `protocolId` |
| POST | `/api/crew/{id}/packets` | `{ alertId?, packet, note? }`: queues an SBAR packet as a LinkMessage (H1, H2) |
| POST | `/api/crew/{id}/protocol-runs`, `/api/crew/protocol-runs/{runId}/steps`, `.../end` | Protocol run log (F3) |
| GET | `/api/ground/inbox` | Messages that have arrived through the link, with their age (E2, H3) |
| GET | `/api/ground/overview` | Crew x metrics status grid (D2) |
| GET | `/api/link/queue` | Every message with its state: held, in_flight or delivered |

## Rules the code follows

- Status = worse of the absolute status and the z-score status. Limits come from `metrics.yaml`; `z_warn` and `z_crit` are per metric.
- Band edges are strict: a value equal to a limit counts as inside it.
- Polarity: `low` flags only falling values, `high` only rising values, `both` either, and `none` (context inputs) has no status.
- Shrunk SD `sqrt((n·sd² + k·sd_pop²)/(n+k))` with k = 10 when n < 10. It needs an optional `sd_pop` per metric in `metrics.yaml`. **No metric has `sd_pop` yet, so no shrinkage happens until it is added.**
- Baselines are locked at mission start. `ApplicationDbContext` throws if a locked baseline is modified or deleted. There is no audit table.
- Link: a message sent while the link is up gets `deliver_at = sent_at + latency`. A message sent while the link is down is held; on reconnect it gets `reconnect time + latency`. Messages already in flight still arrive. `sent_at` never changes.
- Yellow and red in-flight readings create an Alert and send an `alert` LinkMessage to ground (E2).
- Context rules run only for yellow / red in-flight readings. A rule matches if it is enabled, `applies_to` contains the metric, and its `direction` matches the deviation (`status.direction`). Windows count back from the reading's mission time, both ends included. `metric_count` counts distinct mission days (the last `window_days` days, counting today), each day once, using that day's latest reading. The result is stored as JSON on the alert (`Alert.ContextJson`) and returned as `context`.
- Downgrade policy (from `context_rules.yaml`): only `severity_modifier: -1` rules can lower a flag; at most one level in total; by default only yellow can be lowered (red keeps its color but lists the factors); metrics in `never_downgrade_metrics` are never lowered. The alert keeps the original level; `context.downgraded` and `context.downgradedBy` say what changed.
- Environment entries for context rules (`co2_mmhg`, `kp`, `solar_particle_event`) come from `ENVIRONMENT_LOG` (`Kind` is a free string).
- Chest pain trigger: a red reading on `trigger.red_metrics` offers the protocol only if a `trigger.symptoms` symptom was logged within `window_min` minutes of mission time, before or after (both checked when the reading arrives and when the symptom arrives). A trigger symptom on its own always offers the protocol.
- Auth is mocked for the demo. The roles are not enforced yet.
