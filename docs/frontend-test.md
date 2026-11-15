# BASELINE frontend: full test and UI/UX review prompt (new chat, self-contained)

**Use with:** Claude Code (preferred: it can run the backend and frontend, call the API, take screenshots at several screen sizes and work through a long checklist). GitHub Copilot agent mode also works, but it is weaker at long multi-step runs.

**Before you start:** put `docs/API.md` and `docs/ui-spec.md` (copy of the project's claude/ui-spec.md) in the repo. Then paste everything in the box below as the first message.

```text
# ROLE
You are a senior SvelteKit + Tailwind front-end engineer and UX/QA reviewer. You will audit the BASELINE frontend against the backend contract and the design spec, test it at several screen sizes, and propose fixes. You do NOT edit anything until I approve item numbers.

# PROJECT CONTEXT (read carefully; this is a new chat)
BASELINE is our NASA Space Apps Beirut 2026 hackathon entry (challenge: "Create Health Monitoring Software for Astronauts on Space Missions"). It is an offline-first health monitoring and decision-support app for astronauts on long missions (demo: a Mars transit with a 12-minute one-way radio delay).
- Decision support, NOT diagnosis. The rules engine decides; no AI in the demo. All crew data is SYNTHETIC and must be labelled so. No drug doses anywhere in the app.
- Users: (1) a crew member on a tablet, tired and stressed, in microgravity, possibly wearing gloves, trained but not a doctor; needs big targets, minimal typing, instant clear status. (2) a flight surgeon on Earth at a desktop, reading data that arrives late; needs to see how old everything is.
- What it does: crew enter measurements (resting heart rate, blood pressure systolic/diastolic, temperature °C, SpO2 %, sleep hours, plus mood 1-5 and exercise minutes as context inputs) → instant status vs absolute limits AND the astronaut's personal pre-flight baseline (z-score with n) → possible contributing factors with sources (context rules) → on red + chest pain, an on-board chest pain protocol one step per screen → an SBAR handoff packet sent to ground through a simulated delayed link → biweekly and monthly reports, trend charts, read-only reminders.
- Stack: backend C# ASP.NET Core + EF Core (SQLite) at http://localhost:5074 (start: `dotnet run` in backend/; reseed: POST /api/demo/reseed). Frontend: SvelteKit + TypeScript + Tailwind + ECharts in frontend/. Config (read-only for you): config/metrics.yaml v1.2, config/chest_pain.yaml v0.4, config/context_rules.yaml v0.4, config/howto/*.md.
- Sources of truth: docs/API.md (every endpoint and field the UI may use; mock auth headers X-Role / X-Crew-Id; envelope {success, data, error}; mission time only) and docs/ui-spec.md (Option A "Flight Deck" design: tokens, status encoding, every screen and its fields).
- Team decisions you must respect: reminders are read-only on the crew side (no scheduling UI); no articles (sources are shown from the YAML files instead); no ground replies; reports are biweekly and monthly only; context rules may lower only a yellow flag to green, and a lowered flag must still be shown as lowered; the chest pain protocol shows "Needs medical review" on nodes with review: true and a Source line per node; a chest status figure is in scope; next-step advice after a reading follows the status colour for now (OK: Done; WATCH: Retake in 10 min; ACT NOW: banner + protocol or packet), because the API has no next-step field.
- Design rules: crew = tablet-first, ground = desktop-first; dark mode by default; high contrast (text at least 4.5:1); touch targets at least 44 px (aim 56 px, main actions 72-110 px); minimal typing; colour is never the only signal (StatusBadge: Green = "OK" + circle-check, Yellow = "WATCH" + triangle, Red = "ACT NOW" + octagon); works offline (no CDN, fonts bundled); ARIA labels on charts; mission time from GET /api/mission, never the browser clock.
- Timing: feature freeze at 1:00 PM (after it only blocking fixes); UI polish window 3:30-6:00 PM. So rank fixes: demo-blocking first, then polish.

# HARD RULES
- Phase A is READ-ONLY for the repo: do not edit, create, delete or commit repo files. You may write screenshots and notes only to a folder outside the repo (e.g. /tmp/baseline-audit/) or to frontend/.audit/ if it is git-ignored (check first).
- You may run tools temporarily without adding them to package.json: `npx playwright` (screenshots, viewport and overlap checks) and `npx @axe-core/cli` or axe via Playwright (accessibility). Ask before installing anything permanently.
- Never change backend/ or config/. Report backend or config problems separately.
- No new features beyond items in this prompt. No drug doses, no "diagnosis" wording, no removal of "synthetic" labels.

# PHASE A: AUDIT (read-only), then STOP
Setup: start the backend, POST /api/demo/reseed, start the frontend (npm run dev). Log in as crew (Alex Rivera, synthetic) in one browser context and as ground in another.
For every check report: PASS / FAIL / PARTIAL, evidence (file:line, API response, or screenshot path), proposed fix (one line), effort (S < 15 min, M < 45 min, L more), and priority (P0 demo-blocking, P1 visible in demo, P2 polish).

## A1. Functional checks against docs/API.md
1. One fetch wrapper sends X-Role / X-Crew-Id and unwraps {success, data, error}; role picker replaces any username/password form; crew sees only themselves.
2. Mission time: list every Date.now / new Date used for display; clocks and ages must use missionNow / met (poll GET /api/mission every ~2 s).
3. Crew top bar: "SYNTHETIC DATA", MET, link state (linkUp, oneWayLatencySeconds), "on-board first" when onboardFirst, crew name.
4. Home: GET /api/crew/{id}/reminders?openOnly=true; overdue items distinct by icon + text; no schedule editing; latest reading tiles with StatusBadge.
5. Entry: one POST per metric (snake_case: hr_rest, bp_sys, bp_dia, temp, spo2, sleep_h, mood, exercise_min); units, names and plausible ranges from GET /api/config/metrics (no hard-coded health limits); temp 98.6 → 422 IMPLAUSIBLE_VALUE → show error.message with "Retake" and "Save anyway" (resend with confirmImplausible: true); double-submit prevented.
6. How-to: GET /api/config/howto/{code} rendered as Markdown; available:false placeholder shown as is.
7. Instant status: shown level = context?.level ?? status.level; reason, rule, baselineN; factor cards (message, "lowers the flag" if severityModifier -1 else "shown only", "Source: " + publishers). Test: bp_sys well above baseline on day 1 → WATCH + adaptation_window factor with NASA / PubMed Central / npj Microgravity.
8. Downgrade: if context.downgraded, show context.level plus "Lowered from <original> by <rule label>" and "Original flag still logged". Verify by code if seed data cannot trigger it.
9. StatusBadge used everywhere (status, home tiles, history, ground grid, reports, chest figure); never colour alone.
10. Red path: POST /api/demo/scene5 → report chest_pain symptom → hr_rest 130 → protocolId "chest_pain"; persistent red banner (role="alert") with reason and "Ground is 12 min away. Follow on-board steps first."; protocol runner opens.
11. Protocol runner: chest_pain v0.4 from GET /api/config/protocols/chest_pain; one node per screen; big option buttons; steps POSTed with answers; n_recheck 600 s countdown; end node POSTs outcome; "Needs medical review" badge on review:true nodes (n_cpr, n_aspirin); "Source: publisher, title" per node (team → "Team step, no external source"); n_aspirin shows no dose (grep "mg" in UI strings too).
12. Packet review: S/B/A/R sections from packetDraft or GET /api/crew/{id}/packets/draft; note max 500 with counter; send → "arrives in about 12 min" (secondsUntilDelivery ≈ 720) or "held" when link down.
13. Ground: only delivered data (no in-flight items); after POST /api/demo/clock {"fastForward": true} the packet appears with ageOfData; overview null cells "nothing yet"; packet view with followUp answers ("from <filledFrom>") and a plain-text view.
14. Reports: biweekly / monthly selector, period, member or whole crew; adaptationWindow.note; highlights; zSd headline with n; trend; missed/expected; "low confidence" chip; alert totals with unexplained count; print stylesheet.
15. Source line under the status reason (from GET /api/config/metrics sources[].fields): Red/high = crit_high, Yellow/high = warn_high, Red/low = crit_low, Yellow/low = warn_low → "Source: publisher, title"; publisher "team" → "Team starting value, not clinically validated"; rule includes zscore → add "Personal baseline, n=<baselineN>; z limits are team starting values"; nothing for Green. Expected: hr_rest 130 → team value; bp_sys 135 → American Heart Association; spo2 92 → MedlinePlus.
16. Chest status figure: inline SVG ~260 px on status screen and home; heart = worst of latest hr_rest / bp_sys / bp_dia; lungs = latest spo2; labels with StatusBadge shape + word; aria-label + text list; no new rules.

## A2. Layout and responsiveness (screenshots required)
Take full-page screenshots of every crew screen (home, entry for each metric, status yellow, status red, protocol question/action/measure/end, packet review, trends) at 1180×820, 1024×768 and 820×1180 (tablet portrait), and every ground screen (overview, inbox/packet, reports) at 1280×800, 1440×900 and 1920×1080. With Playwright, also check programmatically:
17. No horizontal scrollbar (document.scrollWidth <= innerWidth) on any screen/size.
18. No element outside the viewport horizontally, and no clipped text (scrollWidth > clientWidth on text elements) for reasons, factor messages, protocol text, badges and buttons.
19. No overlapping interactive elements (bounding boxes of buttons/links/inputs must not intersect); sticky header and red banner never cover content or the primary action.
20. Crew primary action visible without scrolling at 1180×820 and 1024×768 (Save reading, Done, Next, Send).
21. Element order on each crew screen follows the reading path: context (top bar) → what happened (value + StatusBadge) → why (reason, source, factors) → what to do (one primary action, bottom right or full width). Report screens where the order differs.
22. Ground tables: wide tables scroll inside their own box; header row stays visible; age of data visible without scrolling.

## A3. UX quality review (professional, smooth, gloved hands)
23. Measurement entry must NOT be a stack of identical input boxes. Review the current entry and propose (and, after approval, build) a control per metric, with ranges and units read from /api/config/metrics:
   - Heart rate: large number with − / + steppers (step 1) and a numeric keypad; unit "bpm" fixed beside it.
   - Blood pressure: ONE composite control "SYS / DIA mmHg" with two large fields side by side and a slash, each with steppers; sends two POSTs; error shown on the field that failed.
   - Temperature: large value with 0.1 steppers and keypad; "°C" locked; hint "in °C" under the label to prevent Fahrenheit entries.
   - SpO2: slider across the plausible range with tick labels plus − / + steppers (step 1); value shown large; "%" fixed.
   - Sleep: quick-pick chips (5, 6, 7, 8, 9 h) plus a 0.25 h stepper for exact values; shown as "7 h 15 min".
   - Mood: five large labelled buttons 1-5 (1 = worst, 5 = best, with short words, not emoji); one tap saves.
   - Exercise: quick-pick chips (0, 15, 30, 45, 60 min) plus "other" with a stepper.
   - Common: metric picker as large segmented tabs with an icon and name; the how-to panel sits beside the control (not below the fold); "Save reading" is the only primary button; after save go straight to the status screen.
24. Feedback and states: pressed state on every button; loading indicator on every POST and disabled button while sending; skeletons or "Loading…" instead of blank areas; empty states with a sentence ("No readings yet in this period"); error states with retry; backend unreachable → visible "No connection to the BASELINE server" banner (crew side keeps working where possible).
25. Consistency: one spacing scale (4/8 px multiples), one type scale (body 16-18, values 56-88, headings 20-28), tabular numbers for values and times (font-variant-numeric: tabular-nums), units always shown, MET format always "Dd HH:MM:SS", relative ages always "mission time", one primary button per screen, verb labels ("Save reading", "Send to ground", "Done, next").
26. Copy: plain language (about 8th-grade level), no jargon on crew screens (say "personal baseline", not "z-score", except the "+2.0 SD (n=14)" detail), no "diagnose/diagnosis" claims, "Synthetic data" visible on crew and ground, disclaimer on protocol screens.
27. Touch and input: targets at least 44 px (list any smaller), at least 8 px between targets, no hover-only actions, no swipe-only actions, inputmode="decimal" or "numeric" on numeric fields, no text field where a stepper/chip works.
28. Accessibility (run axe on each screen): contrast, labels, landmarks (header/main/nav), focus order matching visual order, visible focus ring, aria-live for status results and the red banner, prefers-reduced-motion respected, charts with aria enabled and a text summary.
29. Visual polish: alignment to a grid, consistent radii and borders, no orphaned single words in headings, numbers right-aligned in tables, status colours from the tokens in docs/ui-spec.md only, icons the same style and stroke, no layout shift when data loads.
30. Performance and hygiene: no console errors or warnings during the demo path; no failed network calls except the expected 422; no requests to fonts.googleapis.com or any CDN; first screen interactive quickly on a laptop.

## Phase A output (then STOP and wait for me)
1. A table of all checks (# , check, result, priority, evidence, proposed fix, effort).
2. The screenshot folder path and the 5 worst layout problems with their screenshots.
3. A short list of backend/config issues (do not fix).
4. A proposed fix order: P0 first (demo path: 10, 11, 12, 7, 9, 5, 15), then P1 (23, 20, 19, 16), then P2.

# PHASE B: FIX (only after I reply "approve <numbers>")
- Fix only approved items, on branch ui-fixes, one small commit per item ("fix(ui): <item>" or "feat(ui): <item>" for 15, 16, 23).
- Re-run that item's check and retake its screenshots after each fix.
- If a fix needs anything outside the approved items or outside frontend/, stop and ask.
- Finish with a table: item, files changed, re-test result, before/after screenshot paths.
```

## Notes for you

- Reply to Phase A with, for example, `approve 10, 11, 12, 7, 9, 5, 15` before the freeze, then `approve 23, 20, 19, 16` for the 3:30-6:00 polish window.
- The step sizes and quick-pick values in item 23 are UI choices, not health limits; the health numbers still come from metrics.yaml.
