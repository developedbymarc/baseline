# BASELINE API contract

For the frontend (`/crew` tablet UI and `/ground` desktop UI). Every example below is a real response
captured from the running backend with the seeded demo mission. **All data is synthetic. BASELINE is
decision support, not diagnosis.**

## Basics

| | |
|---|---|
| Base URL | `http://localhost:5074` (launch profile `http`, see `backend/Properties/launchSettings.json`) |
| CORS | Browser calls are allowed from `http://localhost:5173` (Vite dev) and `http://localhost:4173` (Vite preview). Calls made from SvelteKit server code (`+page.server.ts`) are server-to-server and need no CORS. |
| Format | JSON, camelCase property names |
| IDs | GUID strings, e.g. `"330fab86-44dc-4fd8-84f9-3b5d5b48cbc4"` |
| Docs UI | `http://localhost:5074/scalar` (Development only) |
| Offline | No internet needed. `dotnet run` seeds the demo mission if the DB is empty; `dotnet run -- reseed` resets it. |
| Demo data | 4 synthetic crew, 14 pre-flight + 90 in-flight days. The clock starts at launch, so the in-flight history appears as you fast-forward (see `backend/README.md` for the planted events). |

### Response envelope

Every response has the same shape:

```json
{ "success": true, "data": { }, "error": null }
```

On failure `success` is `false`, `data` is `null` and `error` explains why:

```json
{ "success": false, "data": null, "error": { "code": "NO_CREW", "message": "Crew member … not found.", "details": null } }
```

| HTTP | `error.code` examples | Meaning |
|---|---|---|
| 400 | `VALIDATION_FAILURE` (with `details` per field), `UNKNOWN_METRIC`, `BAD_OUTCOME`, `NO_NODE`, `OUT_OF_RANGE`, `BAD_LATENCY`, `BAD_FORMAT` | Bad request |
| 401 | `NO_ROLE`, `NO_330fab86-44dc-4fd8-84f9-3b5d5b48cbc4` | Missing or invalid mock-auth headers (see Roles) |
| 403 | `FORBIDDEN` | Another crew member's data, or a ground-only endpoint |
| 404 | `NO_MISSION`, `NO_CREW`, `NO_ALERT`, `NO_PROTOCOL`, `NO_RUN` | Not found |
| 409 | `ALREADY_INFLIGHT`, `RUN_ENDED`, `BASELINE_LOCKED` | Conflict with current state |
| 422 | `IMPLAUSIBLE_VALUE` | Measurement outside the plausible range: show the message, let the crew member retake or confirm |

### Time

- **Everything is mission time, never the browser clock.** The presenter can fast-forward (1 mission day per 5 s), so read the current time from `GET /api/mission` (`missionNow`) and compute ages from it.
- Timestamps are ISO 8601 in UTC with offset and fractional seconds: `"2026-11-14T06:05:00.99+00:00"`.
- MET (mission elapsed time) is given as `metSeconds` (number, negative before launch) and `met` (display string `"Dd HH:MM:SS"`). Reminders also give `dueMet` in seconds.
- Mission day 1 = the first 24 h after `launchDate`.

### Enum values

| Field | Values |
|---|---|
| `status.level`, `absoluteLevel`, `zLevel`, `context.level`, `context.originalLevel`, alert `level` | `Green`, `Yellow`, `Red` |
| mission `environment` | `Transit`, `Station`, `LunarSurface` |
| mission `mode`, measurement `source` | `Preflight`, `Inflight` |
| measurement `qualityFlag` | `Ok`, `ImplausibleConfirmed` |
| reminder `status` | `Pending`, `Done`, `Late`, `Missed` |
| link message `direction` | `CrewToGround`, `GroundToCrew` |
| link message `type` | `Alert`, `Packet`, `Reply`, `Measurement`, `Symptom`, `ProtocolStep` |
| link message `state` (lowercase) | `held`, `in_flight`, `delivered` |
| `status.rule` (lowercase) | `none`, `threshold`, `zscore`, `threshold+zscore`; alert `ruleId` can also be `symptom` |
| `status.direction` (lowercase) | `high`, `low`, `null` when green |
| protocol offer `matchedBy` | `red_reading`, `red_reading+symptom`, `symptom`, `symptom+red_reading` |

Metric codes come from `config/metrics.yaml`: `hr_rest`, `bp_sys`, `bp_dia`, `temp` (°C), `spo2` (%), `sleep_h` (hours, decimal), and the context inputs `mood` (1 = worst, 5 = best) and `exercise_min`.

## Roles

Mock auth for the demo (the spec asks for mock login only). Send these headers on every crew and ground call:

| Header | Value |
|---|---|
| `X-Role` | `crew` or `ground` (case-insensitive) |
| `X-Crew-Id` | for `crew`: the crew member's `id` from `GET /api/crew` |

Set them once in a fetch wrapper. The login page becomes a role picker: choose ground, or crew plus a crew member.

| Who | Can |
|---|---|
| crew | Read and write **their own** data only (`/api/crew/{their id}/…` and their own protocol runs). `GET /api/crew` returns only themselves. |
| ground | Read any crew member's baselines, measurements and alerts, the ground inbox and overview, but **only data delivered through the link queue**. Ground cannot write crew data and cannot read reminders (reminder progress stays on the crew side). |
| no header | `GET /api/mission`, `/api/config/*`, `/api/link/queue` and all `/api/demo/*` and `POST /api/mission/start` stay open for the presenter. |

**What "delivered" means.** Every in-flight measurement, symptom log and protocol event is synced to ground as a link message (types `Measurement`, `Symptom`, `ProtocolStep`), next to `Alert` and `Packet`. Ground sees a record only after its message arrives:

- measurements: pre-flight data (entered on the ground) plus in-flight readings with `syncedAt` set (`syncedAt` = the time the sync message was delivered)
- alerts: only once their `Alert` message is delivered
- symptoms and protocol steps: as `Symptom` / `ProtocolStep` messages in `GET /api/ground/inbox`
- the overview grid uses only delivered readings

Errors: missing or unknown `X-Role` → 401 `NO_ROLE`; crew without a valid `X-Crew-Id` → 401 `NO_330fab86-44dc-4fd8-84f9-3b5d5b48cbc4`; someone else's data or a ground-only endpoint → 403 `FORBIDDEN`.

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "NO_ROLE",
    "message": "Send the X-Role header: crew or ground.",
    "details": null
  }
}
```

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "FORBIDDEN",
    "message": "Crew members can only access their own data.",
    "details": null
  }
}
```

Ground reading a crew member's alerts after delivery (same shape as the crew view):

```json
{
  "success": true,
  "data": [
    {
      "id": "caed9068-881c-426f-99eb-275bd651f93d",
      "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
      "measurementId": "f856123b-693f-49f5-beb7-84546056532a",
      "level": "Red",
      "reason": "Resting heart rate 130 bpm: Above the critical limit of 120 bpm. +11.3 SD from your personal baseline (n=14).",
      "ruleId": "threshold+zscore",
      "createdAt": "2026-11-14T06:05:01.1863657+00:00",
      "acknowledgedBy": null,
      "context": {
        "originalLevel": "Red",
        "level": "Red",
        "downgraded": false,
        "downgradedBy": [],
        "downgradeBlockedReason": null,
        "factors": [
          {
            "ruleId": "adaptation_window",
            "message": "You are in the first 14 days of the mission. Fluid shifts to the upper body and early heart changes occur while you adapt; the effect on your readings is possible but not established.",
            "severityModifier": 0,
            "sources": [
              {
                "id": "C4",
                "publisher": "NASA",
                "title": "The Heart of the Matter, Cardiovascular Concerns",
                "url": "https://www.nasa.gov/content/cardiovascular-health"
              }
            ]
          }
        ]
      }
    }
  ],
  "error": null
}
```

---

## Mission and demo controls

### GET /api/mission
Role: any. Mission state, mission clock and link. `onboardFirst` is true when the link is down or the one-way latency is over `onboardFirstThresholdSeconds` (story E3): show the on-board protocol first and "notify ground" second.

```json
{
  "success": true,
  "data": {
    "id": "488ca043-618d-43d1-a5fd-5e73c911f77e",
    "name": "Demo Mars transit (SYNTHETIC DATA)",
    "environment": "Transit",
    "launchDate": "2026-11-14T06:00:00+00:00",
    "mode": "Inflight",
    "missionNow": "2026-11-14T06:05:00.8147378+00:00",
    "metSeconds": 300.815,
    "met": "0d 00:05:00",
    "clockRate": 1,
    "clockPaused": false,
    "oneWayLatencySeconds": 720,
    "linkUp": true,
    "onboardFirst": true,
    "onboardFirstThresholdSeconds": 300
  },
  "error": null
}
```

### POST /api/mission/start
Role: open (presenter). Computes and locks every baseline and switches to in-flight. The seed already does this, so it normally returns 409:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "ALREADY_INFLIGHT",
    "message": "The mission has already started; baselines are locked.",
    "details": null
  }
}
```

### POST /api/demo/clock
Role: open (presenter). Any combination of: `fastForward` (true = 1 mission day per 5 s, false = real time), `rate` (mission seconds per real second), `paused`, `advanceSeconds` (jump forward). Returns the mission state.

```json
{ "fastForward": true }
```
```json
{ "advanceSeconds": 1814400 }
```

**Demo tip:** at fast-forward speed 30 mission minutes pass in about 0.1 real seconds, so the chest pain window and the 12-minute link delay are over almost at once. Call `POST /api/demo/scene5` (below) before demo step 5.

Error example (`rate: 0`):

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "OUT_OF_RANGE",
    "message": "Clock rate must be a positive number. (Parameter 'rate')",
    "details": null
  }
}
```

### POST /api/demo/link
Role: open (presenter). `up` toggles the link; `oneWayLatencySeconds` changes the delay for new messages. Returns the mission state.

```json
{ "up": false }
```
```json
{ "up": true, "oneWayLatencySeconds": 720 }
```

### POST /api/demo/scene5
Role: open (presenter). One call to set up demo step 5 (chest pain) from any state: fast-forward **off** (real time, not paused), link **up**, one-way latency **12 minutes**. Messages held while the link was down leave now and arrive 12 minutes later. No body. Returns the mission state; read `missionNow` / `met` for the mission clock time to show on screen.

From here, mission minutes equal real minutes, so a chest pain symptom and a red reading within 30 minutes offer the chest pain protocol, and a packet arrives on ground 12 minutes after it is sent.

```json
{
  "success": true,
  "data": {
    "id": "488ca043-618d-43d1-a5fd-5e73c911f77e",
    "name": "Demo Mars transit (SYNTHETIC DATA)",
    "environment": "Transit",
    "launchDate": "2026-11-14T06:00:00+00:00",
    "mode": "Inflight",
    "missionNow": "2026-11-14T06:35:00.1964298+00:00",
    "metSeconds": 2100.196,
    "met": "0d 00:35:00",
    "clockRate": 1,
    "clockPaused": false,
    "oneWayLatencySeconds": 720,
    "linkUp": true,
    "onboardFirst": true,
    "onboardFirstThresholdSeconds": 300
  },
  "error": null
}
```

### POST /api/demo/reseed
Role: open (presenter). Wipes the database and reloads the synthetic demo mission.

---

## Config (read-only content from config/*.yaml)

### GET /api/config/metrics
Role: any. Units, plausible ranges, limits, z limits, polarity, `sdPop`, sources. Use these for the entry form, the chart bands (`normalLow`/`normalHigh`) and the "how to" link. Do not hard-code health numbers in the UI. Example (first metric, sources trimmed):

```json
[
  {
    "code": "hr_rest",
    "name": "Resting heart rate",
    "unit": "bpm",
    "plausibleMin": 25,
    "plausibleMax": 220,
    "normalLow": 60,
    "normalHigh": 100,
    "warnLow": 50,
    "warnHigh": 100,
    "critLow": 40,
    "critHigh": 120,
    "zWarn": 1.5,
    "zCrit": 2.5,
    "polarity": "both",
    "sdPop": null,
    "instructionsMd": "howto/hr_rest.md",
    "sources": [
      {
        "fields": [
          "normal_low"
        ],
        "publisher": "Mayo Clinic",
        "title": "Heart rate: What's normal?",
        "url": "https://www.mayoclinic.org/heart-rate/expert-answers/faq-20057979",
        "note": "Adult resting 60-100 bpm; very fit athletes closer to 40; see a professional if regularly above 100 or, for non-athletes, often below 60.",
        "checked": "2026-11-14",
        "opened": true
      }
    ],
    "hasStatus": true
  }
]
```

### GET /api/config/howto/{metricCode}
Role: any. The "How to" screen for a metric (story C1): Markdown from the file named in `instructionsMd` (under `config/`, e.g. `config/howto/bp.md`, shared by `bp_sys` and `bp_dia`). While Member Y has not written the file, `available` is false and `markdown` is a placeholder (startup logs a warning, it never fails). Unknown metric → 404 `UNKNOWN_METRIC`.

```json
{
  "success": true,
  "data": {
    "metricCode": "spo2",
    "file": "howto/spo2.md",
    "available": false,
    "markdown": "# Blood oxygen saturation\n\nThe step-by-step guide for this measurement has not been written yet (expected file: config/howto/spo2.md).\n\nUntil it is added, follow your on-board training and the device instructions."
  },
  "error": null
}
```

### GET /api/config/protocols, GET /api/config/protocols/{id}
Role: any. The decision tree for the protocol runner: `start`, `treeYaml` (nodes with `type` question / action / measure / end, `options[].next`, `next`, `timerS`, `buildPacket`, `notifyGround`, `review`), `trigger`, `disclaimer`, `latencyRule` (display text) and `sources`. Example trimmed to two nodes:

```json
{
  "id": "chest_pain",
  "title": "Chest pain / suspected cardiac event",
  "version": "0.3",
  "language": "en",
  "start": "n_start",
  "disclaimer": "BASELINE gives decision support from public first-aid guidance. It does not diagnose. Most chest pain is not cardiac, but it cannot be told apart at the bedside, so act and notify ground.",
  "trigger": {
    "symptoms": [
      "chest_pain"
    ],
    "redMetrics": [
      "hr_rest",
      "bp_sys"
    ],
    "requiresSymptom": true,
    "windowMin": 30
  },
  "latencyRule": "When the link is slow or down, show this on-board protocol first and \"notify ground\" second.",
  "treeYaml": [
    {
      "id": "n_start",
      "type": "question",
      "text": "Is the crew member unresponsive or not breathing normally?",
      "options": [
        {
          "label": "Yes",
          "next": "n_cpr"
        },
        {
          "label": "No",
          "next": "n_rest"
        }
      ],
      "next": null,
      "metrics": [],
      "src": [
        "S1"
      ],
      "outcome": null,
      "logAnswer": false,
      "log": false,
      "timerS": null,
      "buildPacket": false,
      "notifyGround": false,
      "review": false,
      "note": null
    },
    {
      "id": "n_cpr",
      "type": "end",
      "text": "Follow the on-board CPR / defibrillator procedure now. Use the AED's own instructions. Notify ground immediately (packet is sent automatically).",
      "options": [],
      "next": null,
      "metrics": [],
      "src": [
        "S1"
      ],
      "outcome": "emergency_cpr",
      "logAnswer": false,
      "log": false,
      "timerS": null,
      "buildPacket": true,
      "notifyGround": true,
      "review": true,
      "note": null
    }
  ],
  "sources": [
    {
      "id": "S1",
      "publisher": "MedlinePlus (U.S. National Library of Medicine), content by A.D.A.M., Inc.",
      "title": "Heart attack first aid",
      "url": "https://medlineplus.gov/ency/article/000063.htm",
      "note": "Call emergency number; sit, rest, loosen clothing; aspirin only if not allergic and no dangerous condition; nothing by mouth unless a prescribed heart medicine; CPR/AED if unresponsive and not breathing.",
      "reviewed": "2024-10-07",
      "updated": null,
      "checked": "2026-11-14",
      "opened": true
    },
    {
      "id": "S2",
      "publisher": "American Heart Association News",
      "title": "New guidelines help doctors diagnose chest pain - but only if you act",
      "url": "https://heart.org/en/news/2021/10/28/new-guidelines-help-doctors-diagnose-chest-pain-but-only-if-you-act",
      "note": "Sudden chest pain - call for help, do not self-diagnose, go to the nearest hospital; discomfort can be pressure or tightness in shoulders, arms, neck, back, upper abdomen or jaw; most chest pain is not cardiac.",
      "reviewed": null,
      "updated": null,
      "checked": "2026-11-14",
      "opened": true
    }
  ]
}
```

---

## Crew endpoints

### GET /api/crew
Role: crew (own entry), ground (all).

```json
{
  "success": true,
  "data": [
    {
      "id": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
      "name": "Alex Rivera (synthetic)",
      "role": "Commander",
      "age": 45,
      "sex": "M",
      "heightCm": 178,
      "notes": "SYNTHETIC demo crew member. Planted: slow BP drift from mission day 8."
    },
    {
      "id": "576ec6e9-5389-411b-af91-d6e3d82e0034",
      "name": "Jordan Lee (synthetic)",
      "role": "Mission specialist",
      "age": 41,
      "sex": "M",
      "heightCm": 172,
      "notes": "SYNTHETIC demo crew member. Planted: one-day fever on day 52 with a heart-rate rise."
    }
  ],
  "error": null
}
```

### GET /api/crew/{crewId}/baselines
Role: crew (own), ground. `sd` is the personal SD; `sdEffective` is what z-scores use (shrunk toward `sdPop` when `n` < 10, see `shrunk`). Always show `n` next to a z-score. `warning` is set when n < 10 and the metric has no `sdPop`.

```json
{
  "success": true,
  "data": [
    {
      "metricCode": "bp_dia",
      "mean": 70.143,
      "sd": 2.349,
      "sdEffective": 2.349,
      "shrunk": false,
      "n": 14,
      "lockedAt": "2026-11-14T05:59:01.0208425+00:00",
      "warning": null
    },
    {
      "metricCode": "bp_sys",
      "mean": 107.214,
      "sd": 3.446,
      "sdEffective": 3.446,
      "shrunk": false,
      "n": 14,
      "lockedAt": "2026-11-14T05:59:01.0208425+00:00",
      "warning": null
    }
  ],
  "error": null
}
```

### GET /api/crew/{crewId}/measurements?metric=bp_sys&days=7
Role: crew (own), ground (delivered only: pre-flight data and in-flight readings with `syncedAt`). History for charts (story D1). `days` = 7, 14 or 30; omit for the full mission (pre-flight included). In-flight points carry their `status`; pre-flight points have `status: null`.

```json
{
  "success": true,
  "data": [
    {
      "id": "b6528b09-df4c-4986-8425-cb130d809ff6",
      "metricCode": "bp_sys",
      "value": 105,
      "recordedAt": "2026-11-07T08:10:00+00:00",
      "source": "Preflight",
      "qualityFlag": "Ok",
      "note": null,
      "syncedAt": "2026-11-07T08:10:00+00:00",
      "status": null
    }
  ],
  "error": null
}
```

### POST /api/crew/{crewId}/measurements
Role: crew (own). **One metric per call.**

```json
{
  "metricCode": "bp_sys",
  "value": 114.1
}
```

Optional fields: `confirmImplausible` (send `true` to save a value after a 422), `note`.

**Response (yellow, with context factors).** `status.level` is the flag from the status engine. `context` is null when the reading is green; for yellow or red it lists the factors (stories I1, I2) and the downgrade outcome (I4). If `context.downgraded` is true, show `context.level` as the displayed color **and** say it was lowered from `context.originalLevel` by `context.downgradedBy` (never hide the original flag). `alertId` is set for every yellow or red in-flight reading.

```json
{
  "success": true,
  "data": {
    "measurement": {
      "id": "e4d0e311-985d-4e1e-a0cb-1afaac4f3ae5",
      "metricCode": "bp_sys",
      "value": 114.1,
      "recordedAt": "2026-11-14T06:05:01.1133446+00:00",
      "qualityFlag": "Ok"
    },
    "status": {
      "level": "Yellow",
      "absoluteLevel": "Green",
      "zLevel": "Yellow",
      "z": 1.998,
      "sdEffective": 3.446,
      "baselineN": 14,
      "rule": "zscore",
      "reason": "+2.0 SD from your personal baseline (n=14).",
      "direction": "high"
    },
    "context": {
      "originalLevel": "Yellow",
      "level": "Yellow",
      "downgraded": false,
      "downgradedBy": [],
      "downgradeBlockedReason": null,
      "factors": [
        {
          "ruleId": "adaptation_window",
          "message": "You are in the first 14 days of the mission. Fluid shifts to the upper body and early heart changes occur while you adapt; the effect on your readings is possible but not established.",
          "severityModifier": 0,
          "sources": [
            {
              "id": "C4",
              "publisher": "NASA",
              "title": "The Heart of the Matter, Cardiovascular Concerns",
              "url": "https://www.nasa.gov/content/cardiovascular-health"
            },
            {
              "id": "C5",
              "publisher": "PubMed Central",
              "title": "Physiologic changes in short- and long-duration spaceflight (PMC2696527)",
              "url": "https://pmc.ncbi.nlm.nih.gov/articles/PMC2696527/"
            }
          ]
        }
      ]
    },
    "alertId": "239d2c8a-ee61-43c0-8e5c-a1858c290e14",
    "protocolId": null,
    "protocol": null,
    "packetDraft": null
  },
  "error": null
}
```

**Response (yellow, downgraded by a context rule).** Same reading pattern after two short nights on two mission days: `short_sleep` (severity -1) lowers the yellow flag to green. `status.level` stays `Yellow`; `context.level` is `Green` with `downgraded: true` and `downgradedBy: ["short_sleep"]`. The alert is still created and logged.

```json
{
  "measurement": {
    "id": "7cb6f5ad-90a4-4ca6-a83e-dd1491b01f8c",
    "metricCode": "bp_sys",
    "value": 117.5,
    "recordedAt": "2026-11-15T06:35:00.2763392+00:00",
    "qualityFlag": "Ok"
  },
  "status": {
    "level": "Yellow",
    "absoluteLevel": "Green",
    "zLevel": "Yellow",
    "z": 2.006,
    "sdEffective": 4.45,
    "baselineN": 14,
    "rule": "zscore",
    "reason": "+2.0 SD from your personal baseline (n=14).",
    "direction": "high"
  },
  "context": {
    "originalLevel": "Yellow",
    "level": "Yellow",
    "downgraded": false,
    "downgradedBy": [],
    "downgradeBlockedReason": null,
    "factors": [
      {
        "ruleId": "adaptation_window",
        "message": "You are in the first 14 days of the mission. Fluid shifts to the upper body and early heart changes occur while you adapt; the effect on your readings is possible but not established.",
        "severityModifier": 0,
        "sources": [
          {
            "id": "C4",
            "publisher": "NASA",
            "title": "The Heart of the Matter, Cardiovascular Concerns",
            "url": "https://www.nasa.gov/content/cardiovascular-health"
          },
          {
            "id": "C5",
            "publisher": "PubMed Central",
            "title": "Physiologic changes in short- and long-duration spaceflight (PMC2696527)",
            "url": "https://pmc.ncbi.nlm.nih.gov/articles/PMC2696527/"
          },
          {
            "id": "C11",
            "publisher": "NPJ Microgravity (THESEUS review series)",
            "title": "Towards human exploration of space: cardiovascular, respiratory, and renal research priorities (PMC5515532)",
            "url": "https://pmc.ncbi.nlm.nih.gov/articles/PMC5515532"
          }
        ]
      }
    ]
  },
  "alertId": "b3a43b37-d86a-452d-9c66-7473914f7fd9",
  "protocolId": null,
  "protocol": null,
  "packetDraft": null
}
```

The stored alert carries the same object as `context`:

```json
[
  {
    "id": "b3a43b37-d86a-452d-9c66-7473914f7fd9",
    "crewMemberId": "576ec6e9-5389-411b-af91-d6e3d82e0034",
    "measurementId": "7cb6f5ad-90a4-4ca6-a83e-dd1491b01f8c",
    "level": "Yellow",
    "reason": "Systolic blood pressure 117.5 mmHg: +2.0 SD from your personal baseline (n=14).",
    "ruleId": "zscore",
    "createdAt": "2026-11-15T06:35:00.2763392+00:00",
    "acknowledgedBy": null,
    "context": {
      "originalLevel": "Yellow",
      "level": "Yellow",
      "downgraded": false,
      "downgradedBy": [],
      "downgradeBlockedReason": null,
      "factors": [
        {
          "ruleId": "adaptation_window",
          "message": "You are in the first 14 days of the mission. Fluid shifts to the upper body and early heart changes occur while you adapt; the effect on your readings is possible but not established.",
          "severityModifier": 0,
          "sources": [
            {
              "id": "C4",
              "publisher": "NASA",
              "title": "The Heart of the Matter, Cardiovascular Concerns",
              "url": "https://www.nasa.gov/content/cardiovascular-health"
            }
          ]
        }
      ]
    }
  }
]
```

**Response (red, chest pain protocol offered).** A red reading also returns `packetDraft`, the SBAR packet ready for review (see below). When a red reading on `hr_rest`, `bp_sys`, `bp_dia` or `spo2` arrives within 30 mission minutes of a `chest_pain` symptom (either order), `protocolId` is `"chest_pain"` and `protocol` says what matched. Open the protocol runner.

```json
{
  "measurement": {
    "id": "f856123b-693f-49f5-beb7-84546056532a",
    "metricCode": "hr_rest",
    "value": 130,
    "recordedAt": "2026-11-14T06:05:01.1863657+00:00",
    "qualityFlag": "Ok"
  },
  "status": {
    "level": "Red",
    "absoluteLevel": "Red",
    "zLevel": "Red",
    "z": 11.29,
    "sdEffective": 4.201,
    "baselineN": 14,
    "rule": "threshold+zscore",
    "reason": "Above the critical limit of 120 bpm. +11.3 SD from your personal baseline (n=14).",
    "direction": "high"
  },
  "context": {
    "originalLevel": "Red",
    "level": "Red",
    "downgraded": false,
    "downgradedBy": [],
    "downgradeBlockedReason": null,
    "factors": [
      {
        "ruleId": "adaptation_window",
        "message": "You are in the first 14 days of the mission. Fluid shifts to the upper body and early heart changes occur while you adapt; the effect on your readings is possible but not established.",
        "severityModifier": 0,
        "sources": [
          {
            "id": "C4",
            "publisher": "NASA",
            "title": "The Heart of the Matter, Cardiovascular Concerns",
            "url": "https://www.nasa.gov/content/cardiovascular-health"
          }
        ]
      }
    ]
  },
  "alertId": "caed9068-881c-426f-99eb-275bd651f93d",
  "protocolId": "chest_pain",
  "protocol": {
    "protocolId": "chest_pain",
    "matchedBy": "red_reading+symptom",
    "matchedSymptomLogIds": [
      "5aeaafa5-bca3-44af-92bb-89668dee3d00"
    ],
    "matchedRedAlertIds": []
  },
  "packetDraft": "(SBAR packet object, see 'SBAR handoff packet')"
}
```

**Response (implausible value, 422).** Show the message; let the crew member retake or resend with `confirmImplausible: true`.

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "IMPLAUSIBLE_VALUE",
    "message": "98.6 looks like Fahrenheit (98.6 °F = 37.0 °C). This field is in °C.",
    "details": null
  }
}
```

Validation error example (wrong JSON type):

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "VALIDATION_FAILURE",
    "message": "One or more validation errors occurred.",
    "details": {
      "req": [
        "The req field is required."
      ],
      "$.value": [
        "The JSON value could not be converted to backend.Controllers.SubmitMeasurementRequest. Path: $.value | LineNumber: 0 | BytePositionInLine: 15."
      ]
    }
  }
}
```

### GET /api/crew/{crewId}/reminders?openOnly=true
Role: crew (own) only. Due and overdue reminders from the crew-side schedule (works with the link down). `due` = due time reached; `overdue` = still pending after its due time. `openOnly=false` also returns done, late and missed ones.

```json
{
  "success": true,
  "data": [
    {
      "id": "e43dc776-7c49-4a85-afb9-40adc7b7d621",
      "metricCode": "sleep_h",
      "dueAt": "2026-11-14T07:00:00+00:00",
      "dueMet": 3600,
      "status": "Pending",
      "due": false,
      "overdue": false,
      "completedAt": null
    },
    {
      "id": "7334c000-a027-4fa2-ad5a-929dd2584c4a",
      "metricCode": "spo2",
      "dueAt": "2026-11-14T08:00:00+00:00",
      "dueMet": 7200,
      "status": "Pending",
      "due": false,
      "overdue": false,
      "completedAt": null
    }
  ],
  "error": null
}
```

### GET /api/crew/{crewId}/alerts
Role: crew (own), ground (delivered only, see Roles). Newest first. `context` is the stored context-engine result (the `Alert.ContextJson` column) with the same shape as in the measurement response; `null` for symptom alerts. `level` is always the original flag.

```json
{
  "success": true,
  "data": [
    {
      "id": "caed9068-881c-426f-99eb-275bd651f93d",
      "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
      "measurementId": "f856123b-693f-49f5-beb7-84546056532a",
      "level": "Red",
      "reason": "Resting heart rate 130 bpm: Above the critical limit of 120 bpm. +11.3 SD from your personal baseline (n=14).",
      "ruleId": "threshold+zscore",
      "createdAt": "2026-11-14T06:05:01.1863657+00:00",
      "acknowledgedBy": null,
      "context": {
        "originalLevel": "Red",
        "level": "Red",
        "downgraded": false,
        "downgradedBy": [],
        "downgradeBlockedReason": null,
        "factors": [
          {
            "ruleId": "adaptation_window",
            "message": "You are in the first 14 days of the mission. Fluid shifts to the upper body and early heart changes occur while you adapt; the effect on your readings is possible but not established.",
            "severityModifier": 0,
            "sources": [
              {
                "id": "C4",
                "publisher": "NASA",
                "title": "The Heart of the Matter, Cardiovascular Concerns",
                "url": "https://www.nasa.gov/content/cardiovascular-health"
              },
              {
                "id": "C5",
                "publisher": "PubMed Central",
                "title": "Physiologic changes in short- and long-duration spaceflight (PMC2696527)",
                "url": "https://pmc.ncbi.nlm.nih.gov/articles/PMC2696527/"
              }
            ]
          }
        ]
      }
    },
    {
      "id": "1cb1fa53-9ca0-4872-b2be-515e30f8cf7a",
      "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
      "measurementId": null,
      "level": "Red",
      "reason": "Symptom reported: Chest pain / suspected cardiac event.",
      "ruleId": "symptom",
      "createdAt": "2026-11-14T06:05:01.1504001+00:00",
      "acknowledgedBy": null,
      "context": null
    }
  ],
  "error": null
}
```

### POST /api/crew/{crewId}/symptoms
Role: crew (own). A symptom listed in a protocol's `trigger.symptoms` (today: `chest_pain`) raises a red alert and always offers that protocol. Red readings within the window are listed in `protocol.matchedRedAlertIds`.

```json
{
  "symptomCode": "chest_pain",
  "note": "pressure behind sternum"
}
```

```json
{
  "success": true,
  "data": {
    "symptomLogId": "5aeaafa5-bca3-44af-92bb-89668dee3d00",
    "alertId": "1cb1fa53-9ca0-4872-b2be-515e30f8cf7a",
    "protocolId": "chest_pain",
    "protocol": {
      "protocolId": "chest_pain",
      "matchedBy": "symptom",
      "matchedSymptomLogIds": [
        "5aeaafa5-bca3-44af-92bb-89668dee3d00"
      ],
      "matchedRedAlertIds": []
    }
  },
  "error": null
}
```

### POST /api/crew/{crewId}/protocol-runs
Role: crew (own). Starts a run of a protocol; `protocolVersion` is stored so ground knows which tree was used. The response includes `packetDraft` (see "SBAR handoff packet"); step and end responses have `packetDraft: null`.

```json
{
  "protocolId": "chest_pain",
  "alertId": "caed9068-881c-426f-99eb-275bd651f93d"
}
```

```json
{
  "success": true,
  "data": {
    "id": "debb3743-5f9e-4356-8921-2d8f54e961c5",
    "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
    "protocolId": "chest_pain",
    "protocolVersion": "0.3",
    "alertId": "caed9068-881c-426f-99eb-275bd651f93d",
    "startedAt": "2026-11-14T06:05:01.4040836+00:00",
    "endedAt": null,
    "steps": [],
    "packetDraft": "(SBAR packet object, see 'SBAR handoff packet')"
  },
  "error": null
}
```

### POST /api/crew/protocol-runs/{runId}/steps
Role: crew (owner of the run). Logs one step with its mission time (story F3). `nodeId` must exist in the protocol.

```json
{
  "nodeId": "n_start",
  "answer": "No"
}
```

```json
{
  "success": true,
  "data": {
    "id": "debb3743-5f9e-4356-8921-2d8f54e961c5",
    "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
    "protocolId": "chest_pain",
    "protocolVersion": "0.3",
    "alertId": "caed9068-881c-426f-99eb-275bd651f93d",
    "startedAt": "2026-11-14T06:05:01.4040836+00:00",
    "endedAt": null,
    "steps": [
      {
        "nodeId": "n_start",
        "answer": "No",
        "at": "2026-11-14T06:05:01.4724098+00:00"
      }
    ],
    "packetDraft": null
  },
  "error": null
}
```

### POST /api/crew/protocol-runs/{runId}/end
Role: crew (owner of the run). `outcome` must be one of the protocol's end-node outcomes (`emergency_cpr`, `hold_and_wait_for_ground`).

```json
{ "outcome": "hold_and_wait_for_ground" }
```

Error example:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "BAD_OUTCOME",
    "message": "Outcome must be one of the protocol's end outcomes: emergency_cpr, hold_and_wait_for_ground.",
    "details": null
  }
}
```

### SBAR handoff packet (work package 4.5)

The server builds the packet from stored data only (story H1), from one template (`backend/Templates/sbar_packet.sbn`) over one JSON object. The crew never types anything except an optional note (H2).

- **Automatic draft:** a red reading returns `packetDraft` in the measurement response, and starting a protocol returns `packetDraft` in the run response. Show it on the review screen.
- **Preview again:** `GET /api/crew/{crewId}/packets/draft?alertId=…&runId=…` (crew, own) rebuilds it without queueing, e.g. at the protocol's `build_packet` step.
- **Send:** `POST /api/crew/{crewId}/packets` rebuilds the packet at that moment (so it includes every step so far), adds the note and queues it as a link message (`type: Packet`, `alertId`). `payload.createdAt` = `sentAt`.

Packet content (`payload` of the link message, and `packetDraft`):

| Field | Content |
|---|---|
| `schema` | `baseline.sbar.v1` |
| `createdAt`, `createdMet`, `createdUtc` | When it was built (mission time) |
| `crewMemberId`, `crewName`, `alertId`, `runId`, `note` | Who, what it is about, the crew note (max 500 characters, trimmed) |
| `data.trigger` | `kind` (`red_reading`, `symptom`, `protocol`), one-line `summary`, the `alert` (level, reason, rule, reading) and trigger `symptoms` |
| `data.measurements72h` | Readings of the relevant metrics in the last 72 h, with status for in-flight ones. Relevant = the flagged metric plus the metrics of the protocol involved (its `trigger.red_metrics` and measure steps) |
| `data.baselines` | Per relevant metric: mean, `sdEffective`, `n`, `shrunk`, latest value and its z-score, and a one-line `summary` |
| `data.protocol` | Run id, title, version, start, and `steps` with node text, answer and time |
| `data.mission` | Name, environment, latency (seconds and text), link state, mission day, adaptation window |
| `data.contextFactors`, `data.contextOutcome` | The factors stored on the alert and the downgrade outcome |
| `data.followUp` | Pre-answered checklist (H3): `onset`, `medications`, `recent_exercise`, `sleep`, `environmental_events`. Each has `question`, `answer` and `filledFrom` (`symptom log`, `alert`, `protocol run`, `measurements`, `environment log` or `not recorded`) |
| `data.recommendation` | Deterministic text: protocol progress, what is requested, and a warning when the link is down |
| `text` | The whole packet as plain text in SBAR order, for reading aloud over a voice link |

Ground reads everything from this one object; no other call is needed. On ground, show `ageOfData` from the link message (e.g. "Written 14 min ago (mission time)").

#### POST /api/crew/{crewId}/packets
Role: crew (own). At least one of `alertId` or `runId`; with only `runId`, the run's alert is used.

```json
{
  "alertId": "caed9068-881c-426f-99eb-275bd651f93d",
  "runId": "debb3743-5f9e-4356-8921-2d8f54e961c5",
  "note": "felt dizzy"
}
```

Errors: 400 `NO_TRIGGER` (neither id), 400 `NOTE_TOO_LONG`, 404 `NO_ALERT` / `NO_RUN` (not this crew member's).

Response: the queued link message (packet payload trimmed):

```json
{
  "success": true,
  "data": {
    "id": "d1fee45c-75fc-4196-9157-754c0a1d298c",
    "direction": "CrewToGround",
    "type": "Packet",
    "alertId": "caed9068-881c-426f-99eb-275bd651f93d",
    "state": "in_flight",
    "sentAt": "2026-11-14T06:05:01.5196759+00:00",
    "deliverAt": "2026-11-14T06:17:01.5196759+00:00",
    "deliveredAt": null,
    "ageSeconds": 0.013,
    "ageOfData": "Written just now (mission time)",
    "secondsUntilDelivery": 719.987,
    "payload": {
      "schema": "baseline.sbar.v1",
      "createdAt": "2026-11-14T06:05:01.5196759+00:00",
      "createdMet": "0d 00:05:01",
      "createdUtc": "2026-11-14 06:05 UTC",
      "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
      "crewName": "Alex Rivera (synthetic)",
      "alertId": "caed9068-881c-426f-99eb-275bd651f93d",
      "runId": "debb3743-5f9e-4356-8921-2d8f54e961c5",
      "note": "felt dizzy",
      "data": {
        "mission": {
          "name": "Demo Mars transit (SYNTHETIC DATA)",
          "environment": "Transit",
          "oneWayLatencySeconds": 720,
          "oneWayLatencyText": "12 min",
          "linkUp": true,
          "missionDay": 1,
          "inAdaptationWindow": true
        },
        "crew": {
          "id": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
          "name": "Alex Rivera (synthetic)",
          "role": "Commander",
          "age": 45,
          "sex": "M"
        },
        "trigger": {
          "kind": "red_reading",
          "summary": "Red Resting heart rate 130 bpm at 0d 00:05:01 MET.",
          "alert": {
            "id": "caed9068-881c-426f-99eb-275bd651f93d",
            "level": "Red",
            "reason": "Resting heart rate 130 bpm: Above the critical limit of 120 bpm. +11.3 SD from your personal baseline (n=14).",
            "ruleId": "threshold+zscore",
            "at": "2026-11-14T06:05:01.1863657+00:00",
            "atMet": "0d 00:05:01",
            "metricCode": "hr_rest",
            "metricName": "Resting heart rate",
            "value": 130,
            "unit": "bpm"
          },
          "symptoms": [
            {
              "id": "5aeaafa5-bca3-44af-92bb-89668dee3d00",
              "symptomCode": "chest_pain",
              "note": "pressure behind sternum",
              "loggedAt": "2026-11-14T06:05:01.1504001+00:00",
              "loggedMet": "0d 00:05:01"
            }
          ]
        },
        "measurements72h": [
          {
            "metricCode": "hr_rest",
            "metricName": "Resting heart rate",
            "unit": "bpm",
            "value": 77,
            "recordedAt": "2026-11-11T08:05:00+00:00",
            "recordedMet": "-2d 21:55:00",
            "level": null,
            "rule": null
          }
        ],
        "baselines": [
          {
            "metricCode": "hr_rest",
            "metricName": "Resting heart rate",
            "unit": "bpm",
            "mean": 82.571,
            "sdEffective": 4.201,
            "n": 14,
            "shrunk": false,
            "latestValue": 130,
            "latestAt": "2026-11-14T06:05:01.1863657+00:00",
            "latestZ": 11.29,
            "summary": "Resting heart rate: baseline 82.6 bpm, SD 4.2, n=14; latest 130 bpm (+11.3 SD)"
          }
        ],
        "protocol": {
          "runId": "debb3743-5f9e-4356-8921-2d8f54e961c5",
          "protocolId": "chest_pain",
          "title": "Chest pain / suspected cardiac event",
          "version": "0.3",
          "startedAt": "2026-11-14T06:05:01.4040836+00:00",
          "startedMet": "0d 00:05:01",
          "endedAt": null,
          "steps": [
            {
              "nodeId": "n_start",
              "text": "Is the crew member unresponsive or not breathing normally?",
              "answer": "No",
              "at": "2026-11-14T06:05:01.4724098+00:00",
              "atMet": "0d 00:05:01"
            }
          ]
        },
        "contextFactors": [
          {
            "ruleId": "adaptation_window",
            "message": "You are in the first 14 days of the mission. Fluid shifts to the upper body and early heart changes occur while you adapt; the effect on your readings is possible but not established.",
            "severityModifier": 0,
            "sources": [
              {
                "id": "C4",
                "publisher": "NASA",
                "title": "The Heart of the Matter, Cardiovascular Concerns",
                "url": "https://www.nasa.gov/content/cardiovascular-health"
              }
            ]
          }
        ],
        "contextOutcome": {
          "originalLevel": "Red",
          "level": "Red",
          "downgraded": false,
          "downgradedBy": []
        },
        "followUp": [
          {
            "key": "onset",
            "question": "Onset time?",
            "answer": "First 'chest_pain' logged at 0d 00:05:01 MET (just before this packet).",
            "filledFrom": "symptom log"
          }
        ],
        "recommendation": "On-board protocol in progress (Chest pain / suspected cardiac event); last step: Is the crew member unresponsive or not breathing normally. Request: flight surgeon review of the readings and steps above, with instructions to the crew."
      },
      "text": "(plain-text packet, see the example text below)"
    }
  },
  "error": null
}
```

Example `text` from the packet above:

```text
BASELINE HANDOFF PACKET (SBAR) - SYNTHETIC DATA - decision support, not diagnosis
Written 0d 00:05:01 MET (2026-11-14 06:05 UTC) by Alex Rivera (synthetic)

SITUATION
Red Resting heart rate 130 bpm at 0d 00:05:01 MET.
Flag: Red (threshold+zscore) - Resting heart rate 130 bpm: Above the critical limit of 120 bpm. +19.6 SD from your personal baseline (n=14).
Symptom: chest_pain logged 0d 00:05:01 MET - "pressure behind sternum"
Crew note: "felt dizzy"

BACKGROUND
Crew member: Alex Rivera (synthetic), Commander, 45 y, M
Mission: Demo Mars transit (SYNTHETIC DATA), Transit, mission day 1 (adaptation window)
Link: one-way delay 12 min, link up
Baselines (pre-flight):
  - Resting heart rate: baseline 81.8 bpm, SD 2.46, n=14; latest 130 bpm (+19.6 SD)
  - Systolic blood pressure: baseline 102.1 mmHg, SD 4.42, n=14; latest 111 mmHg (+2.0 SD)
  - Diastolic blood pressure: baseline 69.9 mmHg, SD 2.76, n=14; latest 67 mmHg (-1.1 SD)
  - Blood oxygen saturation: baseline 97.9 %, SD 0.66, n=14; latest 98 % (+0.2 SD)
Readings, last 72 h:
  - -2d 21:56:10 MET  Systolic blood pressure 107 mmHg
  - -2d 21:55:05 MET  Blood oxygen saturation 98 %
  - -2d 21:41:30 MET  Resting heart rate 84 bpm
  - -2d 21:39:16 MET  Diastolic blood pressure 75 mmHg
  - -1d 21:59:45 MET  Resting heart rate 83 bpm
  - -1d 21:46:48 MET  Diastolic blood pressure 71 mmHg
  - -1d 21:07:11 MET  Blood oxygen saturation 99 %
  - -1d 21:05:11 MET  Systolic blood pressure 94 mmHg
  - -0d 21:47:01 MET  Diastolic blood pressure 67 mmHg
  - -0d 21:37:28 MET  Systolic blood pressure 107 mmHg
  - -0d 21:15:05 MET  Resting heart rate 84 bpm
  - -0d 21:03:24 MET  Blood oxygen saturation 98 %
  - 0d 00:05:00 MET  Systolic blood pressure 102 mmHg [Green]
  - 0d 00:05:00 MET  Systolic blood pressure 111 mmHg [Yellow]
  - 0d 00:05:01 MET  Resting heart rate 130 bpm [Red]

ASSESSMENT
Possible contributing factors (not a diagnosis):
  - adaptation_window: You are in the first 14 days of the mission. Fluid shifts to the upper body and early heart changes occur while you adapt; the effect on your readings is possible but not established.
Protocol: Chest pain / suspected cardiac event v0.3, started 0d 00:05:01 MET
  - 0d 00:05:01 MET  Is the crew member unresponsive or not breathing normally? -> No
Follow-up questions, pre-answered:
  - Onset time? First 'chest_pain' logged at 0d 00:05:01 MET (just before this packet).
  - Medications taken? No medication step logged.
  - Recent exercise? day -1: 434 min; day 0: 349 min.
  - Sleep? day -1: 7.9 h; day 0: 8.1 h.
  - Environmental events? None logged in the last 72 h.

RECOMMENDATION
On-board protocol in progress (Chest pain / suspected cardiac event); last step: Is the crew member unresponsive or not breathing normally. Request: flight surgeon review of the readings and steps above, with instructions to the crew.
```

---

## Link (latency simulator)

### Link message shape

Used by `/api/link/queue`, `/api/ground/inbox` and the packet response.

| Field | Meaning |
|---|---|
| `sentAt` | Original creation time (mission time). Never changes. |
| `deliverAt` | `sentAt` + latency, or reconnect time + latency for messages held while the link was down. `null` while held. |
| `deliveredAt` | Equals `deliverAt` once delivered. |
| `state` | `held`, `in_flight` or `delivered` |
| `ageSeconds` | `missionNow - sentAt`: how stale the data is (story E2, H3) |
| `ageOfData` | The same in plain language: "Written 14 min ago (mission time)" |
| `secondsUntilDelivery` | For in-flight messages; `null` otherwise |
| `payload` | The message body: alert details, or the packet |

### Sync message payloads

| `type` | `payload` keys |
|---|---|
| `Measurement` | `measurementId`, `crewMemberId`, `crewName`, `metricCode`, `value`, `unit`, `recordedAt`, `source`, `qualityFlag`, `note`, `level`, `rule`, `reason` |
| `Symptom` | `symptomLogId`, `crewMemberId`, `crewName`, `symptomCode`, `note`, `loggedAt` |
| `ProtocolStep` | `runId`, `crewMemberId`, `protocolId`, `protocolVersion`, `event` (`started`, `step`, `ended`), `nodeId`, `answer`, `outcome`, `at` (keys always present; unused ones are `null`) |
| `Alert` | `alertId`, `crewMemberId`, `crewName`, `metricCode` or `symptomCode`, `value`, `unit`, `level`, `rule`, `reason`, `z`, `baselineN`, `recordedAt`, `context`, `protocolId` |
| `Packet` | the SBAR packet, see "SBAR handoff packet" |

### GET /api/link/queue?type=Packet,Alert
Role: open (presenter view). Every message in every state, newest first, so the demo can show the queue draining. `type` (optional, comma-separated) filters by message type.

```json
{
  "success": true,
  "data": [
    {
      "id": "d1fee45c-75fc-4196-9157-754c0a1d298c",
      "direction": "CrewToGround",
      "type": "Packet",
      "alertId": "caed9068-881c-426f-99eb-275bd651f93d",
      "state": "in_flight",
      "sentAt": "2026-11-14T06:05:01.5196759+00:00",
      "deliverAt": "2026-11-14T06:17:01.5196759+00:00",
      "deliveredAt": null,
      "ageSeconds": 0.047,
      "ageOfData": "Written just now (mission time)",
      "secondsUntilDelivery": 719.953,
      "payload": {
        "schema": "baseline.sbar.v1",
        "createdAt": "2026-11-14T06:05:01.5196759+00:00",
        "createdMet": "0d 00:05:01",
        "createdUtc": "2026-11-14 06:05 UTC",
        "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
        "crewName": "Alex Rivera (synthetic)",
        "alertId": "caed9068-881c-426f-99eb-275bd651f93d",
        "runId": "debb3743-5f9e-4356-8921-2d8f54e961c5",
        "note": "felt dizzy",
        "data": {
          "mission": {
            "name": "Demo Mars transit (SYNTHETIC DATA)",
            "environment": "Transit",
            "oneWayLatencySeconds": 720,
            "oneWayLatencyText": "12 min",
            "linkUp": true,
            "missionDay": 1,
            "inAdaptationWindow": true
          },
          "crew": {
            "id": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
            "name": "Alex Rivera (synthetic)",
            "role": "Commander",
            "age": 45,
            "sex": "M"
          },
          "trigger": {
            "kind": "red_reading",
            "summary": "Red Resting heart rate 130 bpm at 0d 00:05:01 MET.",
            "alert": {
              "id": "caed9068-881c-426f-99eb-275bd651f93d",
              "level": "Red",
              "reason": "Resting heart rate 130 bpm: Above the critical limit of 120 bpm. +11.3 SD from your personal baseline (n=14).",
              "ruleId": "threshold+zscore",
              "at": "2026-11-14T06:05:01.1863657+00:00",
              "atMet": "0d 00:05:01",
              "metricCode": "hr_rest",
              "metricName": "Resting heart rate",
              "value": 130,
              "unit": "bpm"
            },
            "symptoms": [
              {
                "id": "5aeaafa5-bca3-44af-92bb-89668dee3d00",
                "symptomCode": "chest_pain",
                "note": "pressure behind sternum",
                "loggedAt": "2026-11-14T06:05:01.1504001+00:00",
                "loggedMet": "0d 00:05:01"
              }
            ]
          },
          "measurements72h": [
            {
              "metricCode": "hr_rest",
              "metricName": "Resting heart rate",
              "unit": "bpm",
              "value": 77,
              "recordedAt": "2026-11-11T08:05:00+00:00",
              "recordedMet": "-2d 21:55:00",
              "level": null,
              "rule": null
            }
          ],
          "baselines": [
            {
              "metricCode": "hr_rest",
              "metricName": "Resting heart rate",
              "unit": "bpm",
              "mean": 82.571,
              "sdEffective": 4.201,
              "n": 14,
              "shrunk": false,
              "latestValue": 130,
              "latestAt": "2026-11-14T06:05:01.1863657+00:00",
              "latestZ": 11.29,
              "summary": "Resting heart rate: baseline 82.6 bpm, SD 4.2, n=14; latest 130 bpm (+11.3 SD)"
            }
          ],
          "protocol": {
            "runId": "debb3743-5f9e-4356-8921-2d8f54e961c5",
            "protocolId": "chest_pain",
            "title": "Chest pain / suspected cardiac event",
            "version": "0.3",
            "startedAt": "2026-11-14T06:05:01.4040836+00:00",
            "startedMet": "0d 00:05:01",
            "endedAt": null,
            "steps": [
              {
                "nodeId": "n_start",
                "text": "Is the crew member unresponsive or not breathing normally?",
                "answer": "No",
                "at": "2026-11-14T06:05:01.4724098+00:00",
                "atMet": "0d 00:05:01"
              }
            ]
          },
          "contextFactors": [
            {
              "ruleId": "adaptation_window",
              "message": "You are in the first 14 days of the mission. Fluid shifts to the upper body and early heart changes occur while you adapt; the effect on your readings is possible but not established.",
              "severityModifier": 0,
              "sources": [
                {
                  "id": "C4",
                  "publisher": "NASA",
                  "title": "The Heart of the Matter, Cardiovascular Concerns",
                  "url": "https://www.nasa.gov/content/cardiovascular-health"
                }
              ]
            }
          ],
          "contextOutcome": {
            "originalLevel": "Red",
            "level": "Red",
            "downgraded": false,
            "downgradedBy": []
          },
          "followUp": [
            {
              "key": "onset",
              "question": "Onset time?",
              "answer": "First 'chest_pain' logged at 0d 00:05:01 MET (just before this packet).",
              "filledFrom": "symptom log"
            }
          ],
          "recommendation": "On-board protocol in progress (Chest pain / suspected cardiac event); last step: Is the crew member unresponsive or not breathing normally. Request: flight surgeon review of the readings and steps above, with instructions to the crew."
        },
        "text": "(plain-text packet, see the example text below)"
      }
    }
  ],
  "error": null
}
```

---

## Ground endpoints

### GET /api/ground/inbox?type=Packet,Alert
Role: ground. Only messages that have **arrived** through the link, newest first, with the age of the data. The inbox includes the sync messages (`Measurement`, `Symptom`, `ProtocolStep`); filter with `type` for a packet / alert view. Unknown type → 400 `BAD_TYPE`.

```json
{
  "success": true,
  "data": [
    {
      "id": "d1fee45c-75fc-4196-9157-754c0a1d298c",
      "direction": "CrewToGround",
      "type": "Packet",
      "alertId": "caed9068-881c-426f-99eb-275bd651f93d",
      "state": "delivered",
      "sentAt": "2026-11-14T06:05:01.5196759+00:00",
      "deliverAt": "2026-11-14T06:17:01.5196759+00:00",
      "deliveredAt": "2026-11-14T06:17:01.5196759+00:00",
      "ageSeconds": 1621.139,
      "ageOfData": "Written 27 min ago (mission time)",
      "secondsUntilDelivery": null,
      "payload": {
        "schema": "baseline.sbar.v1",
        "createdAt": "2026-11-14T06:05:01.5196759+00:00",
        "createdMet": "0d 00:05:01",
        "createdUtc": "2026-11-14 06:05 UTC",
        "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
        "crewName": "Alex Rivera (synthetic)",
        "alertId": "caed9068-881c-426f-99eb-275bd651f93d",
        "runId": "debb3743-5f9e-4356-8921-2d8f54e961c5",
        "note": "felt dizzy",
        "data": {
          "mission": {
            "name": "Demo Mars transit (SYNTHETIC DATA)",
            "environment": "Transit",
            "oneWayLatencySeconds": 720,
            "oneWayLatencyText": "12 min",
            "linkUp": true,
            "missionDay": 1,
            "inAdaptationWindow": true
          },
          "crew": {
            "id": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
            "name": "Alex Rivera (synthetic)",
            "role": "Commander",
            "age": 45,
            "sex": "M"
          },
          "trigger": {
            "kind": "red_reading",
            "summary": "Red Resting heart rate 130 bpm at 0d 00:05:01 MET.",
            "alert": {
              "id": "caed9068-881c-426f-99eb-275bd651f93d",
              "level": "Red",
              "reason": "Resting heart rate 130 bpm: Above the critical limit of 120 bpm. +11.3 SD from your personal baseline (n=14).",
              "ruleId": "threshold+zscore",
              "at": "2026-11-14T06:05:01.1863657+00:00",
              "atMet": "0d 00:05:01",
              "metricCode": "hr_rest",
              "metricName": "Resting heart rate",
              "value": 130,
              "unit": "bpm"
            },
            "symptoms": [
              {
                "id": "5aeaafa5-bca3-44af-92bb-89668dee3d00",
                "symptomCode": "chest_pain",
                "note": "pressure behind sternum",
                "loggedAt": "2026-11-14T06:05:01.1504001+00:00",
                "loggedMet": "0d 00:05:01"
              }
            ]
          },
          "measurements72h": [
            {
              "metricCode": "hr_rest",
              "metricName": "Resting heart rate",
              "unit": "bpm",
              "value": 77,
              "recordedAt": "2026-11-11T08:05:00+00:00",
              "recordedMet": "-2d 21:55:00",
              "level": null,
              "rule": null
            }
          ],
          "baselines": [
            {
              "metricCode": "hr_rest",
              "metricName": "Resting heart rate",
              "unit": "bpm",
              "mean": 82.571,
              "sdEffective": 4.201,
              "n": 14,
              "shrunk": false,
              "latestValue": 130,
              "latestAt": "2026-11-14T06:05:01.1863657+00:00",
              "latestZ": 11.29,
              "summary": "Resting heart rate: baseline 82.6 bpm, SD 4.2, n=14; latest 130 bpm (+11.3 SD)"
            }
          ],
          "protocol": {
            "runId": "debb3743-5f9e-4356-8921-2d8f54e961c5",
            "protocolId": "chest_pain",
            "title": "Chest pain / suspected cardiac event",
            "version": "0.3",
            "startedAt": "2026-11-14T06:05:01.4040836+00:00",
            "startedMet": "0d 00:05:01",
            "endedAt": null,
            "steps": [
              {
                "nodeId": "n_start",
                "text": "Is the crew member unresponsive or not breathing normally?",
                "answer": "No",
                "at": "2026-11-14T06:05:01.4724098+00:00",
                "atMet": "0d 00:05:01"
              }
            ]
          },
          "contextFactors": [
            {
              "ruleId": "adaptation_window",
              "message": "You are in the first 14 days of the mission. Fluid shifts to the upper body and early heart changes occur while you adapt; the effect on your readings is possible but not established.",
              "severityModifier": 0,
              "sources": [
                {
                  "id": "C4",
                  "publisher": "NASA",
                  "title": "The Heart of the Matter, Cardiovascular Concerns",
                  "url": "https://www.nasa.gov/content/cardiovascular-health"
                }
              ]
            }
          ],
          "contextOutcome": {
            "originalLevel": "Red",
            "level": "Red",
            "downgraded": false,
            "downgradedBy": []
          },
          "followUp": [
            {
              "key": "onset",
              "question": "Onset time?",
              "answer": "First 'chest_pain' logged at 0d 00:05:01 MET (just before this packet).",
              "filledFrom": "symptom log"
            }
          ],
          "recommendation": "On-board protocol in progress (Chest pain / suspected cardiac event); last step: Is the crew member unresponsive or not breathing normally. Request: flight surgeon review of the readings and steps above, with instructions to the crew."
        },
        "text": "(plain-text packet, see the example text below)"
      }
    }
  ],
  "error": null
}
```

### GET /api/ground/overview
Role: ground. Crew x metrics grid (story D2) with the latest **delivered** in-flight status per cell (`null` cells = nothing delivered yet).

```json
{
  "success": true,
  "data": [
    {
      "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
      "name": "Alex Rivera (synthetic)",
      "role": "Commander",
      "cells": [
        {
          "metricCode": "hr_rest",
          "value": 130,
          "recordedAt": "2026-11-14T06:05:01.1863657+00:00",
          "status": {
            "level": "Red",
            "absoluteLevel": "Red",
            "zLevel": "Red",
            "z": 11.29,
            "sdEffective": 4.201,
            "baselineN": 14,
            "rule": "threshold+zscore",
            "reason": "Above the critical limit of 120 bpm. +11.3 SD from your personal baseline (n=14).",
            "direction": "high"
          }
        }
      ]
    }
  ],
  "error": null
}
```

---

## Reports (biweekly and monthly)

Fixed periods counted from launch (work package 4.4, stories D3, D4, I5): **biweekly** = mission days 1-14, 15-28, …; **monthly** = 1-30, 31-60, 61-90. There is no weekly report. When a period ends it is stored in REPORT (per member and for the whole crew) the next time any report endpoint is called. The current period is computed on request and marked `complete: false`.

`{window}` is `biweekly` or `monthly`; `{period}` is a number (1 = first) or `current`.

### GET /api/reports/periods?window=biweekly
Role: open. The periods so far.

```json
{
  "success": true,
  "data": [
    {
      "window": "Biweekly",
      "number": 1,
      "start": "2026-11-14T06:00:00+00:00",
      "end": "2026-11-28T06:00:00+00:00",
      "startDay": 1,
      "endDay": 14,
      "complete": false
    }
  ],
  "error": null
}
```

### GET /api/crew/{crewId}/reports/{window}/{period}
Role: crew (own; built from all crew-side data, `view: "crew"`), ground (any member; built **only from delivered data**, `view: "ground"`).

### GET /api/reports/crew/{window}/{period}
Role: ground only. Every member's rows plus crew-level highlights and alert totals.

**Report fields**

| Field | Meaning |
|---|---|
| `startDay`, `endDay`, `periodStart`, `periodEnd`, `complete` | The period (mission days and mission time) |
| `view`, `dataAsOf`, `dataAsOfMet` | Whose data and up to when |
| `adaptationWindow` | `overlaps` is true when the period includes mission days 1-14; `note` explains it |
| `parameters` | Trend and confidence settings from appsettings (`TrendMaxPValue` 0.05, `TrendMinEffectSd` 0.5, `MinPointsForConfidence` 5) |
| `members[].rows[]` | One row per metric (see below) |
| `members[].alerts`, `alertsTotal` | Story I5: `measurementAlerts`, `withContextFactors`, `withoutContextFactors` (unexplained), `symptomAlerts`, `unexplainedAlertIds` |
| `highlights` | Plain-language sentences from fixed templates (deterministic, no AI) |

**Metric row**

| Field | Meaning |
|---|---|
| `windowN`, `windowMean` | In-flight readings in the period and their mean |
| `baselineMean`, `baselineN`, `sdEffective`, `baselineShrunk`, `baselineSource` | Baseline (`locked` at mission start; context inputs use pre-flight data, `preflight`) |
| `delta` | `windowMean - baselineMean` |
| `zSd` | **Headline:** SDs of normal variation, `delta / sdEffective` |
| `zShift` | "Is the average shifted": `delta / (sdEffective / sqrt(windowN))` |
| `trendSlopePerDay`, `trendChangeOverWindow`, `trendPValue`, `trend` | Theil-Sen slope and Mann-Kendall p. `trend` is `rising` / `falling` only if p < 0.05 and the change over the window is at least 0.5 × `sdEffective`; otherwise `stable`; `insufficient` below 3 readings |
| `missed`, `expected` | Scheduled measurements missed / due in the period (same rule as the crew reminders) |
| `lowConfidence` | Fewer than 5 readings in the window |

Example (member, current biweekly period, rows trimmed):

```json
{
  "success": true,
  "data": {
    "scope": "Member",
    "window": "Biweekly",
    "period": 1,
    "periodStart": "2026-11-14T06:00:00+00:00",
    "periodEnd": "2026-11-28T06:00:00+00:00",
    "startDay": 1,
    "endDay": 14,
    "complete": false,
    "view": "crew",
    "dataAsOf": "2026-11-15T06:35:00.3255381+00:00",
    "dataAsOfMet": "1d 00:35:00",
    "adaptationWindow": {
      "days": 14,
      "overlaps": true,
      "note": "This period includes the adaptation window (mission days 1-14); early shifts are expected and drift alerts are not raised for them."
    },
    "parameters": {
      "trendMaxPValue": 0.05,
      "trendMinEffectSd": 0.5,
      "minPointsForConfidence": 5,
      "adaptationWindowDaysFallback": 14
    },
    "members": [
      {
        "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
        "name": "Alex Rivera (synthetic)",
        "role": "Commander",
        "rows": [
          {
            "metricCode": "hr_rest",
            "metricName": "Resting heart rate",
            "unit": "bpm",
            "windowN": 2,
            "windowMean": 108.5,
            "baselineMean": 82.571,
            "baselineN": 14,
            "sdEffective": 4.201,
            "baselineShrunk": false,
            "baselineSource": "locked",
            "delta": 25.929,
            "zSd": 6.172,
            "zShift": 8.729,
            "trendSlopePerDay": -516.085,
            "trendChangeOverWindow": -7225.191,
            "trendPValue": null,
            "trend": "insufficient",
            "missed": 0,
            "expected": 1,
            "lowConfidence": true
          },
          {
            "metricCode": "bp_sys",
            "metricName": "Systolic blood pressure",
            "unit": "mmHg",
            "windowN": 3,
            "windowMean": 109.033,
            "baselineMean": 107.214,
            "baselineN": 14,
            "sdEffective": 3.446,
            "baselineShrunk": false,
            "baselineSource": "locked",
            "delta": 1.819,
            "zSd": 0.528,
            "zShift": 0.914,
            "trendSlopePerDay": -11.522,
            "trendChangeOverWindow": -161.302,
            "trendPValue": 1.0,
            "trend": "stable",
            "missed": 0,
            "expected": 1,
            "lowConfidence": true
          }
        ],
        "missedTotal": 0,
        "alerts": {
          "measurementAlerts": 2,
          "withContextFactors": 2,
          "withoutContextFactors": 0,
          "symptomAlerts": 1,
          "unexplainedAlertIds": []
        },
        "highlights": [
          "Resting heart rate averaged 108.5 bpm, now +6.2 SD above baseline (n=14).",
          "Resting heart rate: only 2 readings in this period, low confidence."
        ]
      }
    ],
    "alertsTotal": {
      "measurementAlerts": 2,
      "withContextFactors": 2,
      "withoutContextFactors": 0,
      "symptomAlerts": 1,
      "unexplainedAlertIds": []
    },
    "highlights": [
      "This period includes the adaptation window (mission days 1-14); early shifts are expected and drift alerts are not raised for them.",
      "Resting heart rate averaged 108.5 bpm, now +6.2 SD above baseline (n=14)."
    ]
  },
  "error": null
}
```

Example (whole crew, monthly, ground view, trimmed):

```json
{
  "scope": "Crew",
  "window": "Monthly",
  "period": 1,
  "periodStart": "2026-11-14T06:00:00+00:00",
  "periodEnd": "2026-12-14T06:00:00+00:00",
  "startDay": 1,
  "endDay": 30,
  "complete": false,
  "view": "ground",
  "dataAsOf": "2026-11-15T06:35:00.3979514+00:00",
  "dataAsOfMet": "1d 00:35:00",
  "adaptationWindow": {
    "days": 14,
    "overlaps": true,
    "note": "This period includes the adaptation window (mission days 1-14); early shifts are expected and drift alerts are not raised for them."
  },
  "parameters": {
    "trendMaxPValue": 0.05,
    "trendMinEffectSd": 0.5,
    "minPointsForConfidence": 5,
    "adaptationWindowDaysFallback": 14
  },
  "members": [
    {
      "crewMemberId": "330fab86-44dc-4fd8-84f9-3b5d5b48cbc4",
      "name": "Alex Rivera (synthetic)",
      "role": "Commander",
      "rows": [
        {
          "metricCode": "hr_rest",
          "metricName": "Resting heart rate",
          "unit": "bpm",
          "windowN": 2,
          "windowMean": 108.5,
          "baselineMean": 82.571,
          "baselineN": 14,
          "sdEffective": 4.201,
          "baselineShrunk": false,
          "baselineSource": "locked",
          "delta": 25.929,
          "zSd": 6.172,
          "zShift": 8.729,
          "trendSlopePerDay": -516.085,
          "trendChangeOverWindow": -15482.551,
          "trendPValue": null,
          "trend": "insufficient",
          "missed": 0,
          "expected": 1,
          "lowConfidence": true
        }
      ],
      "missedTotal": 0,
      "alerts": {
        "measurementAlerts": 2,
        "withContextFactors": 2,
        "withoutContextFactors": 0,
        "symptomAlerts": 1,
        "unexplainedAlertIds": []
      },
      "highlights": [
        "Resting heart rate averaged 108.5 bpm, now +6.2 SD above baseline (n=14)."
      ]
    }
  ],
  "alertsTotal": {
    "measurementAlerts": 5,
    "withContextFactors": 3,
    "withoutContextFactors": 2,
    "symptomAlerts": 1,
    "unexplainedAlertIds": [
      "e2672f32-6c02-4d3d-a587-60caab371a67"
    ]
  },
  "highlights": [
    "This period includes the adaptation window (mission days 1-14); early shifts are expected and drift alerts are not raised for them."
  ]
}
```

---

## What the frontend mock data must change

Checked against `frontend/src` on this branch (SvelteKit, not React as the spec says):

1. **One metric per POST, snake_case codes.** `MeasurementEntrySchema` posts `hrRest`, `bpSys`, `bpDia`, `temp`, `spo2`, `sleepH` together. The API takes `{ "metricCode": "hr_rest", "value": 72 }` per reading, so the form should send one request per filled field (codes: `hr_rest`, `bp_sys`, `bp_dia`, `temp`, `spo2`, `sleep_h`). Each response has its own status color.
2. **`sleepH` is required in the yup schema but the form has no sleep input**, so the form can never validate. Either add the field or make it optional.
3. **Units:** `temp` in °C (98.6 returns 422 with a Fahrenheit hint), `spo2` in %, `sleep_h` in decimal hours, `mood` 1 to 5 with 5 = best.
4. **Handle 422 `IMPLAUSIBLE_VALUE`:** show `error.message`, offer "retake" or "confirm" (resend with `confirmImplausible: true`).
5. **Colors:** read `status.level` (`Green` / `Yellow` / `Red`, PascalCase). If `context.downgraded`, display `context.level` and the note "lowered from {originalLevel} by {downgradedBy}". Always add an icon and text, never color alone.
6. **Time:** use `missionNow` / `met` from `GET /api/mission` for clocks, ages and "due" labels; never `Date.now()`. Timestamps are UTC ISO strings with `+00:00`.
7. **Enums:** PascalCase (`Inflight`, `Preflight`, `Transit`, `Pending`, `CrewToGround`, `Packet`) except `state`, `rule` and `direction`, which are lowercase strings.
8. **Envelope:** every response is `{ success, data, error }`; read `data`.
9. **Protocol steps** are an array `steps: [{ nodeId, answer, at }]`.
10. **Login page:** replace the username/password form with the mock role picker described under Roles, and send `X-Role` / `X-Crew-Id` on every call.
12. **Ground views lag by the link delay:** a reading the crew just entered appears for ground only after `oneWayLatencySeconds` (12 min in the demo mission) or after reconnect. Show `ageSeconds` / `syncedAt`, not "live".
11. **Offline (not mock data, but a spec rule):** `frontend/src/lib/styles/globals.scss` imports Lato from Google Fonts. The crew UI must work without internet (spec section 6), so bundle the font locally.
