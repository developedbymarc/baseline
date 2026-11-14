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
| IDs | GUID strings, e.g. `"676ba813-0f7f-48bf-97cb-728da239a832"` |
| Docs UI | `http://localhost:5074/scalar` (Development only) |
| Offline | No internet needed. `dotnet run` seeds the demo mission if the DB is empty; `dotnet run -- reseed` resets it. |

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
| 401 | `NO_ROLE`, `NO_676ba813-0f7f-48bf-97cb-728da239a832` | Missing or invalid mock-auth headers (see Roles) |
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

Errors: missing or unknown `X-Role` → 401 `NO_ROLE`; crew without a valid `X-Crew-Id` → 401 `NO_676ba813-0f7f-48bf-97cb-728da239a832`; someone else's data or a ground-only endpoint → 403 `FORBIDDEN`.

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
      "id": "9d950f80-239c-4f38-91f5-ed9fe53d5dcf",
      "crewMemberId": "676ba813-0f7f-48bf-97cb-728da239a832",
      "measurementId": "9efba5f2-7f92-4edd-98db-6fd43322d545",
      "level": "Red",
      "reason": "Resting heart rate 130 bpm: Above the critical limit of 120 bpm. +19.6 SD from your personal baseline (n=14).",
      "ruleId": "threshold+zscore",
      "createdAt": "2026-11-14T06:05:01.038939+00:00",
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
    "id": "cecac03c-d4f7-427c-a452-1beac0af031b",
    "name": "Demo Mars transit (SYNTHETIC DATA)",
    "environment": "Transit",
    "launchDate": "2026-11-14T06:00:00+00:00",
    "mode": "Inflight",
    "missionNow": "2026-11-14T06:05:00.5966646+00:00",
    "metSeconds": 300.597,
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
      "id": "676ba813-0f7f-48bf-97cb-728da239a832",
      "name": "Alex Rivera (synthetic)",
      "role": "Commander",
      "age": 45,
      "sex": "M",
      "heightCm": 178,
      "notes": null
    },
    {
      "id": "a1ba8468-4d8d-4ea6-ab09-3c9590675a94",
      "name": "Jordan Lee (synthetic)",
      "role": "Mission specialist",
      "age": 41,
      "sex": "M",
      "heightCm": 172,
      "notes": null
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
      "mean": 69.929,
      "sd": 2.759,
      "sdEffective": 2.759,
      "shrunk": false,
      "n": 14,
      "lockedAt": "2026-11-13T06:00:00.5370562+00:00",
      "warning": null
    },
    {
      "metricCode": "bp_sys",
      "mean": 102.143,
      "sd": 4.418,
      "sdEffective": 4.418,
      "shrunk": false,
      "n": 14,
      "lockedAt": "2026-11-13T06:00:00.5370562+00:00",
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
      "id": "1e74aae4-a1ab-48a9-9eaa-7e38a38fc907",
      "metricCode": "bp_sys",
      "value": 101,
      "recordedAt": "2026-11-07T08:43:05.4290793+00:00",
      "source": "Preflight",
      "qualityFlag": "Ok",
      "note": null,
      "syncedAt": "2026-11-07T08:43:05.4290793+00:00",
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
  "value": 111.0
}
```

Optional fields: `confirmImplausible` (send `true` to save a value after a 422), `note`.

**Response (yellow, with context factors).** `status.level` is the flag from the status engine. `context` is null when the reading is green; for yellow or red it lists the factors (stories I1, I2) and the downgrade outcome (I4). If `context.downgraded` is true, show `context.level` as the displayed color **and** say it was lowered from `context.originalLevel` by `context.downgradedBy` (never hide the original flag). `alertId` is set for every yellow or red in-flight reading.

```json
{
  "success": true,
  "data": {
    "measurement": {
      "id": "bf6377ad-2e57-44f1-be75-44b509e1fd88",
      "metricCode": "bp_sys",
      "value": 111,
      "recordedAt": "2026-11-14T06:05:00.9264148+00:00",
      "qualityFlag": "Ok"
    },
    "status": {
      "level": "Yellow",
      "absoluteLevel": "Green",
      "zLevel": "Yellow",
      "z": 2.005,
      "sdEffective": 4.418,
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
    "alertId": "0de4391b-005f-44f2-9a21-cadaba7fbad9",
    "protocolId": null,
    "protocol": null
  },
  "error": null
}
```

**Response (yellow, downgraded by a context rule).** Same reading pattern after two short nights on two mission days: `short_sleep` (severity -1) lowers the yellow flag to green. `status.level` stays `Yellow`; `context.level` is `Green` with `downgraded: true` and `downgradedBy: ["short_sleep"]`. The alert is still created and logged.

```json
{
  "measurement": {
    "id": "96227d43-6b16-4a3f-afa3-eca4c0b9ae30",
    "metricCode": "bp_sys",
    "value": 110.8,
    "recordedAt": "2026-11-15T06:38:38.4615802+00:00",
    "qualityFlag": "Ok"
  },
  "status": {
    "level": "Yellow",
    "absoluteLevel": "Green",
    "zLevel": "Yellow",
    "z": 2.014,
    "sdEffective": 2.525,
    "baselineN": 14,
    "rule": "zscore",
    "reason": "+2.0 SD from your personal baseline (n=14).",
    "direction": "high"
  },
  "context": {
    "originalLevel": "Yellow",
    "level": "Green",
    "downgraded": true,
    "downgradedBy": [
      "short_sleep"
    ],
    "downgradeBlockedReason": null,
    "factors": [
      {
        "ruleId": "short_sleep",
        "message": "Short sleep (under 6 h on at least 2 of the last 3 nights) can raise blood pressure and resting heart rate.",
        "severityModifier": -1,
        "sources": [
          {
            "id": "C1",
            "publisher": "Mayo Clinic / NHLBI",
            "title": "Prolonged Shortened Sleep Increases Blood Pressure at Night",
            "url": "https://newsnetwork.mayoclinic.org/discussion/prolonged-shortened-sleep-increases-blood-pressure-at-night-mayo-clinic-researchers-find/"
          },
          {
            "id": "C2",
            "publisher": "NHLBI",
            "title": "NHLBI-funded Study Links Insufficient Sleep to Heart Disease",
            "url": "https://www.nhlbi.nih.gov/news/2016/nhlbi-funded-study-links-insufficient-sleep-heart-disease"
          }
        ]
      },
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
  "alertId": "0fd84771-aad2-4832-9365-e5491fa45e1d",
  "protocolId": null,
  "protocol": null
}
```

The stored alert carries the same object as `context`:

```json
[
  {
    "id": "0fd84771-aad2-4832-9365-e5491fa45e1d",
    "crewMemberId": "a1ba8468-4d8d-4ea6-ab09-3c9590675a94",
    "measurementId": "96227d43-6b16-4a3f-afa3-eca4c0b9ae30",
    "level": "Yellow",
    "reason": "Systolic blood pressure 110.8 mmHg: +2.0 SD from your personal baseline (n=14).",
    "ruleId": "zscore",
    "createdAt": "2026-11-15T06:38:38.4615802+00:00",
    "acknowledgedBy": null,
    "context": {
      "originalLevel": "Yellow",
      "level": "Green",
      "downgraded": true,
      "downgradedBy": [
        "short_sleep"
      ],
      "downgradeBlockedReason": null,
      "factors": [
        {
          "ruleId": "short_sleep",
          "message": "Short sleep (under 6 h on at least 2 of the last 3 nights) can raise blood pressure and resting heart rate.",
          "severityModifier": -1,
          "sources": [
            {
              "id": "C1",
              "publisher": "Mayo Clinic / NHLBI",
              "title": "Prolonged Shortened Sleep Increases Blood Pressure at Night",
              "url": "https://newsnetwork.mayoclinic.org/discussion/prolonged-shortened-sleep-increases-blood-pressure-at-night-mayo-clinic-researchers-find/"
            }
          ]
        }
      ]
    }
  }
]
```

**Response (red, chest pain protocol offered).** When a red reading on `hr_rest`, `bp_sys`, `bp_dia` or `spo2` arrives within 30 mission minutes of a `chest_pain` symptom (either order), `protocolId` is `"chest_pain"` and `protocol` says what matched. Open the protocol runner.

```json
{
  "measurement": {
    "id": "9efba5f2-7f92-4edd-98db-6fd43322d545",
    "metricCode": "hr_rest",
    "value": 130,
    "recordedAt": "2026-11-14T06:05:01.038939+00:00",
    "qualityFlag": "Ok"
  },
  "status": {
    "level": "Red",
    "absoluteLevel": "Red",
    "zLevel": "Red",
    "z": 19.638,
    "sdEffective": 2.455,
    "baselineN": 14,
    "rule": "threshold+zscore",
    "reason": "Above the critical limit of 120 bpm. +19.6 SD from your personal baseline (n=14).",
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
  "alertId": "9d950f80-239c-4f38-91f5-ed9fe53d5dcf",
  "protocolId": "chest_pain",
  "protocol": {
    "protocolId": "chest_pain",
    "matchedBy": "red_reading+symptom",
    "matchedSymptomLogIds": [
      "d8c3d6fe-b2c9-449b-a87d-e46425afec60"
    ],
    "matchedRedAlertIds": []
  }
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
      "id": "5244eccc-f640-477d-9756-006a0f44cd18",
      "metricCode": "sleep_h",
      "dueAt": "2026-11-14T07:00:00+00:00",
      "dueMet": 3600,
      "status": "Pending",
      "due": false,
      "overdue": false,
      "completedAt": null
    },
    {
      "id": "a52679f5-9aec-4aa3-9cee-a0077be55f1e",
      "metricCode": "bp_sys",
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
      "id": "9d950f80-239c-4f38-91f5-ed9fe53d5dcf",
      "crewMemberId": "676ba813-0f7f-48bf-97cb-728da239a832",
      "measurementId": "9efba5f2-7f92-4edd-98db-6fd43322d545",
      "level": "Red",
      "reason": "Resting heart rate 130 bpm: Above the critical limit of 120 bpm. +19.6 SD from your personal baseline (n=14).",
      "ruleId": "threshold+zscore",
      "createdAt": "2026-11-14T06:05:01.038939+00:00",
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
      "id": "be2c4058-66cf-4ef5-9649-dcadcff22303",
      "crewMemberId": "676ba813-0f7f-48bf-97cb-728da239a832",
      "measurementId": null,
      "level": "Red",
      "reason": "Symptom reported: Chest pain / suspected cardiac event.",
      "ruleId": "symptom",
      "createdAt": "2026-11-14T06:05:01.004846+00:00",
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
    "symptomLogId": "d8c3d6fe-b2c9-449b-a87d-e46425afec60",
    "alertId": "be2c4058-66cf-4ef5-9649-dcadcff22303",
    "protocolId": "chest_pain",
    "protocol": {
      "protocolId": "chest_pain",
      "matchedBy": "symptom",
      "matchedSymptomLogIds": [
        "d8c3d6fe-b2c9-449b-a87d-e46425afec60"
      ],
      "matchedRedAlertIds": []
    }
  },
  "error": null
}
```

### POST /api/crew/{crewId}/protocol-runs
Role: crew (own). Starts a run of a protocol; `protocolVersion` is stored so ground knows which tree was used.

```json
{
  "protocolId": "chest_pain",
  "alertId": "9d950f80-239c-4f38-91f5-ed9fe53d5dcf"
}
```

```json
{
  "success": true,
  "data": {
    "id": "b7af05e6-3af3-4c23-a4cf-0a4944b7102d",
    "crewMemberId": "676ba813-0f7f-48bf-97cb-728da239a832",
    "protocolId": "chest_pain",
    "protocolVersion": "0.3",
    "alertId": "9d950f80-239c-4f38-91f5-ed9fe53d5dcf",
    "startedAt": "2026-11-14T06:05:01.1286492+00:00",
    "endedAt": null,
    "steps": []
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
    "id": "b7af05e6-3af3-4c23-a4cf-0a4944b7102d",
    "crewMemberId": "676ba813-0f7f-48bf-97cb-728da239a832",
    "protocolId": "chest_pain",
    "protocolVersion": "0.3",
    "alertId": "9d950f80-239c-4f38-91f5-ed9fe53d5dcf",
    "startedAt": "2026-11-14T06:05:01.1286492+00:00",
    "endedAt": null,
    "steps": [
      {
        "nodeId": "n_start",
        "answer": "No",
        "at": "2026-11-14T06:05:01.1714365+00:00"
      }
    ]
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

### POST /api/crew/{crewId}/packets
Role: crew (own). Queues an SBAR handoff packet as a link message (`type: Packet`, linked to `alertId`). It is delivered after the one-way latency; while the link is down it is `held` and leaves on reconnect. `sentAt` is the original creation time and never changes.

```json
{
  "alertId": "9d950f80-239c-4f38-91f5-ed9fe53d5dcf",
  "packet": {
    "situation": "chest pain, HR 130"
  },
  "note": "felt dizzy"
}
```

```json
{
  "success": true,
  "data": {
    "id": "a56487c5-ca33-4128-a4c1-e6cd794594ea",
    "direction": "CrewToGround",
    "type": "Packet",
    "alertId": "9d950f80-239c-4f38-91f5-ed9fe53d5dcf",
    "state": "in_flight",
    "sentAt": "2026-11-14T06:05:01.2015958+00:00",
    "deliverAt": "2026-11-14T06:17:01.2015958+00:00",
    "deliveredAt": null,
    "ageSeconds": 0.004,
    "secondsUntilDelivery": 719.996,
    "payload": {
      "crewMemberId": "676ba813-0f7f-48bf-97cb-728da239a832",
      "crewName": "Alex Rivera (synthetic)",
      "createdAt": "2026-11-14T06:05:01.2015958+00:00",
      "note": "felt dizzy",
      "packet": {
        "situation": "chest pain, HR 130"
      }
    }
  },
  "error": null
}
```

Today `packet` is free JSON from the client. The SBAR packet builder (work package 4.5) will build it on the server; this section will be updated then.

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
| `secondsUntilDelivery` | For in-flight messages; `null` otherwise |
| `payload` | The message body: alert details, or the packet |

### GET /api/link/queue?type=Packet,Alert
Role: open (presenter view). Every message in every state, newest first, so the demo can show the queue draining. `type` (optional, comma-separated) filters by message type.

```json
{
  "success": true,
  "data": [
    {
      "id": "a56487c5-ca33-4128-a4c1-e6cd794594ea",
      "direction": "CrewToGround",
      "type": "Packet",
      "alertId": "9d950f80-239c-4f38-91f5-ed9fe53d5dcf",
      "state": "in_flight",
      "sentAt": "2026-11-14T06:05:01.2015958+00:00",
      "deliverAt": "2026-11-14T06:17:01.2015958+00:00",
      "deliveredAt": null,
      "ageSeconds": 0.041,
      "secondsUntilDelivery": 719.959,
      "payload": {
        "crewMemberId": "676ba813-0f7f-48bf-97cb-728da239a832",
        "crewName": "Alex Rivera (synthetic)",
        "createdAt": "2026-11-14T06:05:01.2015958+00:00",
        "note": "felt dizzy",
        "packet": {
          "situation": "chest pain, HR 130"
        }
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
      "id": "a56487c5-ca33-4128-a4c1-e6cd794594ea",
      "direction": "CrewToGround",
      "type": "Packet",
      "alertId": "9d950f80-239c-4f38-91f5-ed9fe53d5dcf",
      "state": "delivered",
      "sentAt": "2026-11-14T06:05:01.2015958+00:00",
      "deliverAt": "2026-11-14T06:17:01.2015958+00:00",
      "deliveredAt": "2026-11-14T06:17:01.2015958+00:00",
      "ageSeconds": 1621.137,
      "secondsUntilDelivery": null,
      "payload": {
        "crewMemberId": "676ba813-0f7f-48bf-97cb-728da239a832",
        "crewName": "Alex Rivera (synthetic)",
        "createdAt": "2026-11-14T06:05:01.2015958+00:00",
        "note": "felt dizzy",
        "packet": {
          "situation": "chest pain, HR 130"
        }
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
      "crewMemberId": "676ba813-0f7f-48bf-97cb-728da239a832",
      "name": "Alex Rivera (synthetic)",
      "role": "Commander",
      "cells": [
        {
          "metricCode": "hr_rest",
          "value": 130,
          "recordedAt": "2026-11-14T06:05:01.038939+00:00",
          "status": {
            "level": "Red",
            "absoluteLevel": "Red",
            "zLevel": "Red",
            "z": 19.638,
            "sdEffective": 2.455,
            "baselineN": 14,
            "rule": "threshold+zscore",
            "reason": "Above the critical limit of 120 bpm. +19.6 SD from your personal baseline (n=14).",
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
