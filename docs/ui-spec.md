# BASELINE UI spec and Claude Code prompt (for Member B, crew screens; ground views for Member D)

Sun Nov 15, 2026 · prepared by Member X · sketches: canvas "BASELINE UI options" (Option A, recommended)
Stack: **SvelteKit** + Tailwind + ECharts (team decision Nov 15; replaces React in the spec). Backend: docs/API.md, base `http://localhost:5074`. All data synthetic; decision support, not diagnosis.

## 0. Timing and ownership (agree with B and D first)

| When | What | Who |
|---|---|---|
| Before 1:00 PM freeze (only what is missing, no restyle) | (1) status screen: shown colour from `context.level`, "lowered from … by …" note, factor cards with a Source line; (2) protocol runner: "Needs medical review" badge and Source line; (3) red alert banner; (4) read-only reminders on home | B (6.3, 6.5 with D, 6.6) |
| 9.7, 3:30-6:00 PM (polish after Semi Finals feedback) | Apply the Option A look: tokens, status badge, layout | B crew, D ground |
| Never in this build | Reminder scheduling UI (B1), ground replies, articles, body figure unless B confirmed it at noon | |

## 1. Options and the pick

| Option | Idea | For | Against |
|---|---|---|---|
| **A · Flight Deck (recommended)** | Big value and a status block that pairs shape, word and colour; factors as cards beside it; one large next action | Readable at arm's length, gloved; shows downgrade and sources without scrolling | Most layout work of the three |
| B · Procedure card | One numbered card, like a flight procedure page | Fastest to build; text-first | Weak hierarchy; the status is easy to miss |
| C · MET ribbon | Status as the last point on a 7-day chart with the baseline band | Teaches "personal baseline" well | The chart needs ECharts work; hard to read under stress |

Use A for all crew screens; reuse C's chart on the Trends screen (6.4).

## 2. Design tokens (Tailwind theme)

- Colours (dark by default): bg `#0A0F1C`, surface `#121A2B`, raised `#1A2C4D`, border `#2A3550`, text `#EEF2FA`, muted `#A7B0C4`, action `#8FB8FF` (text on it `#0A0F1C`), ok `#43D18A`, watch `#F5B031`, act `#FF6B6B` (text `#FF8A8A`), act surface `#3A1418`, watch surface `#2A2010`, ok surface `#0F2A1E`.
- Type: Inter (body), Space Grotesk (numbers, headings). **Bundle locally** with `@fontsource/inter` and `@fontsource/space-grotesk`, and remove the Google Fonts Lato import in `globals.scss` (offline rule, API.md note 11).
- Sizes: body 16-18 px; values 56-88 px; touch targets at least 56 px on crew screens (spec minimum 44), main actions 72-110 px; radius 12-18 px.

### Status encoding (never colour alone)

| API level | Word | Shape icon | Colour |
|---|---|---|---|
| `Green` | OK | circle with check | ok |
| `Yellow` | WATCH | triangle with ! | watch |
| `Red` | ACT NOW | octagon with ! | act |

Shown level = `context?.level ?? status.level`. If `context.downgraded`, also show "Lowered from {originalLevel word} by {rule names}" and "Original flag still logged and sent to ground". Rule names: map `ruleId` to a label (`short_sleep` → Short sleep, `high_stress` → Stress, `recent_fever` → Recent fever, `adaptation_window` → Adaptation window, `missed_exercise` → Missed exercise, `space_weather_kp` → Geomagnetic storm, `space_weather_spe` → Solar particle event, `elevated_co2` → Cabin CO2).

## 3. Crew screens (tablet, landscape 1180×820 frame; works down to 1024 px)

Every crew screen has the top bar: BASELINE, "SYNTHETIC DATA" chip, `met` from `GET /api/mission` (poll every 2 s; never `Date.now()`), link text from `linkUp` + `oneWayLatencySeconds` ("Link up · 12 min one-way" / "Link down"), "on-board first" when `onboardFirst`, crew name.

**3.1 Home** (`/crew`)
- Due now: `GET /api/crew/{id}/reminders?openOnly=true`. Card per item: metric name (from `/api/config/metrics`), "Due {dueMet as MET}" or "Overdue" when `overdue` (watch surface + clock icon). Tap → Entry for that metric. Footer line: "Schedule set before launch. It keeps running with the link down." No edit controls.
- Latest readings: 4 tiles (HR, BP, temp, SpO2): last point of `GET /api/crew/{id}/measurements?metric={code}&days=7`, with its status badge (small).
- Buttons: New measurement (primary), Trends, Report a symptom (act outline) → `POST /api/crew/{id}/symptoms {symptomCode:"chest_pain", note?}`; if `protocolId` returned → red banner + protocol runner.

**3.2 Measurement entry**
- Metric chips (one per code; BP shows two fields, sent as two POSTs `bp_sys`, `bp_dia`). Value: big input plus − / + steppers (hr 1, bp 1, temp 0.1, spo2 1, sleep 0.25; mood = five buttons 1-5, 5 = best). Unit and label from config (`unit`, `name`); numeric keypad (`inputmode="decimal"`).
- How-to panel: `GET /api/config/howto/{code}` → render `markdown`; if `available` is false show the placeholder text as given.
- `POST /api/crew/{id}/measurements {metricCode, value}`. On 422 `IMPLAUSIBLE_VALUE`: watch box with `error.message`, buttons Retake (clears) and Save anyway (resend with `confirmImplausible: true`).

**3.3 Instant status** (after each POST; Option A layout)
- Left: metric name, value + unit, status block (§2), `status.reason`, "Rule fired: {status.rule}", baseline n.
- Right "Why it may have moved": one card per `context.factors[]`: label from `ruleId`, "lowers the flag" if `severityModifier` is -1 else "shown only", `message`, "Source: {sources[].publisher joined by ·}". No factors → "No known factor found. Ground will see this as unexplained."
- Next action (API has no next-step field, see §6): Green → Done; Yellow → "Retake in 10 min" + Done; Red → red banner, and Open protocol if `protocolId`, else Review packet (`packetDraft`).

**3.4 Red alert banner** (persistent at top while an alert is open)
- Act surface, octagon icon, "ACT NOW", the alert `reason` (or symptom text), and when `onboardFirst`: "Ground is {latency text} away. Follow on-board steps first." Stays until the protocol run ends or the packet is sent. `role="alert"`.

**3.5 Protocol runner** (one node per screen; D owns 6.5, B styles)
- `GET /api/config/protocols/chest_pain`; start `POST /api/crew/{id}/protocol-runs {protocolId, alertId}`; each answer `POST /api/crew/protocol-runs/{runId}/steps {nodeId, answer}`; end node `POST …/end {outcome}`.
- Node types: question → one big button per `options[].label` (min 110 px); action → "Done, next"; measure → open Entry for `metrics[]`, then continue; end → show text, end run; `timerS` → countdown, Next disabled until 0.
- Above the text: "Needs medical review" badge (dashed watch border + triangle) when `review` is true. Below: "Source: {publisher, title}" from `src` ids → `sources[]` ("team" → "Team step, no external source").
- `notifyGround` / `buildPacket` → Review packet (`GET /api/crew/{id}/packets/draft?alertId&runId`). Notify ground button is secondary when `onboardFirst`, primary otherwise. Header: title, `version`, step count. Footer: `disclaimer`.

**3.6 Packet review**
- Four boxes S, B, A, R from the packet object: S = `data.trigger.summary` + symptoms; B = crew, mission day, adaptation window, `data.baselines[].summary`; A = `data.contextFactors` ("not a diagnosis") + protocol steps; R = `data.recommendation`.
- Note: textarea, counter n/500. Send → `POST /api/crew/{id}/packets {alertId, runId, note}`; then "Sent. Arrives in about {secondsUntilDelivery as min} min." (state `in_flight`), or "Held until the link returns" when `state` is `held`.

**3.7 Trends** (6.4): ECharts line per metric, `markArea` for normal band (`normalLow`/`normalHigh`, label "normal band, chart only") and baseline band (mean ± `zWarn`×`sdEffective` from `/baselines`), points coloured and shaped by status; 7 / 14 / 30 / full buttons; `aria: { enabled: true }` plus a text summary under the chart.

## 4. Ground screens (desktop, fluid 1280-1440; D owns 7.1-7.3)

- Always-visible strip: "Everything here is what has arrived through the link. Crew data is at least {latency} old." Ground never shows in-flight messages.
- **Overview** `GET /api/ground/overview`: crew × metric table; cell = status word + shape + value + age (`missionNow − recordedAt`); `null` → "nothing yet"; downgraded → "OK*" with footnote. Right column: inbox items.
- **Inbox / packet view** `GET /api/ground/inbox?type=Packet,Alert`: top bar with `ageOfData` large, sent / arrived MET, delay; S, B, A, R boxes; readings 72 h table; right panel `data.followUp[]` (question, answer, "from {filledFrom}"); button "Show plain text" → `payload.text` in a `<pre>`.
- **Reports** `GET /api/reports/periods?window=`; member `GET /api/crew/{id}/reports/{window}/{period}`, crew `GET /api/reports/crew/{window}/{period}`. Controls: Biweekly / Monthly, period, member or whole crew, Print (`window.print()` with print CSS). Show `adaptationWindow.note`, `highlights`, table (metric, windowMean (windowN), baselineMean (baselineN), delta, **zSd** headline, trend arrow + word, missed / expected, "low confidence" chip when `lowConfidence`), alert totals (I5) with "Unexplained: n".

## 5. Accessibility and behaviour

- Real `<button>`, `<a>`, `<label for>`; `aria-label` on icon-only controls; icons `aria-hidden` when a word sits beside them.
- Contrast at least 4.5:1 (tokens above pass on bg and surface). No hover-only actions; no swipe-only actions.
- Offline: no CDN at runtime (fonts bundled, ECharts from npm).
- Mock auth: role picker sets `X-Role` / `X-Crew-Id` in one fetch wrapper; read `data` from the `{success, data, error}` envelope.

## 6. Known gaps (raise, do not invent)

1. **Next step (story C4)** should come from a rules file, but the API returns none. Interim: fixed mapping by level (§3.3). Say so on the limitations slide, or C adds a `nextStep` field after the event.
2. Body figure only if B confirmed at noon (decision 11B).
3. Spec section 9 and the project instructions still say React; X updates them to SvelteKit.

---

## 7. Claude Code prompt (paste into Claude Code in the repo root)

```text
Role: You are a senior SvelteKit + Tailwind front-end engineer working in this repo's frontend/ folder (SvelteKit, TypeScript). Backend contract: docs/API.md (read it fully first). Design spec: docs/ui-spec.md (this file; read it fully). Do not change backend code or config/*.yaml.

Goal: implement the BASELINE crew screens in the Option A "Flight Deck" style for a NASA Space Apps demo. Feature freeze is 1:00 PM, so work in two phases and stop after Phase 1 for review.

Context: users are a tired crew member in microgravity, possibly gloved (big targets, minimal typing), and a flight surgeon on Earth reading data that arrives 12 minutes late. All data is synthetic; the app is decision support, not diagnosis. Never hard-code health numbers: read units, limits and bands from GET /api/config/metrics. Use mission time from GET /api/mission (missionNow, met), never Date.now().

Phase 1 (functional gaps only, keep the current styling):
1. Status after a measurement: shown level = context?.level ?? status.level. If context.downgraded, show "Lowered from <original> by <rule labels>" and "Original flag still logged". List context.factors as cards: label from ruleId (mapping in spec §2), "lowers the flag" if severityModifier is -1 else "shown only", message, and "Source: " + sources[].publisher joined by " · ".
2. Protocol runner: show a "Needs medical review" badge when a node has review: true, and a "Source:" line from the node's src ids mapped to the protocol's sources (id "team" -> "Team step, no external source").
3. Red alert banner (role="alert") at the top of crew screens while a red alert or chest pain alert is open; text from the alert reason; when onboardFirst add "Ground is <latency> away. Follow on-board steps first."
4. Home: read-only "Due now" list from GET /api/crew/{id}/reminders?openOnly=true (overdue items highlighted), no edit controls.
5. A StatusBadge component used everywhere: Green = OK + circle-check icon, Yellow = WATCH + triangle icon, Red = ACT NOW + octagon icon; colour is never the only signal.
Stop, list changed files, and run the app against the backend to confirm each item with the seeded demo (POST /api/demo/scene5, log chest pain, enter hr_rest 130).

Phase 2 (after Phase 1 is approved; styling only):
6. Add the tokens from spec §2 to tailwind.config (colors, fonts), bundle fonts with @fontsource/inter and @fontsource/space-grotesk, and remove the Google Fonts import from src/lib/styles/globals.scss.
7. Restyle Home, Entry, Status, Protocol runner and Packet review to spec §3 (layouts as in the sketches): top bar with MET and link state, 56 px minimum targets, 72-110 px main actions, dark theme.
8. Trends: ECharts with markArea for the normal band and the personal baseline band, aria.enabled true, and a text summary below.

Constraints: TypeScript; one fetch wrapper that sends X-Role / X-Crew-Id and unwraps {success, data, error}; handle 422 IMPLAUSIBLE_VALUE with Retake / Save anyway (confirmImplausible: true); no new npm packages except @fontsource/*; no network at runtime; keyboard and screen-reader friendly (real buttons and labels, aria-label on icon-only buttons); do not add features outside the spec (no reminder scheduling, no articles, no ground replies). If something in the spec conflicts with docs/API.md, follow API.md and tell me.

Output: small commits per item on a new branch ui-option-a, a short summary of what changed, and any API gaps you hit.
```
