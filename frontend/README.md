# BASELINE frontend

SvelteKit 5 + Tailwind CSS 4 + ECharts. Crew screens (tablet) and ground screens (desktop) in the
Option A "Flight Deck" style from `ui-spec.md`. All data is synthetic; decision support, not diagnosis.

```sh
npm install
npm run dev          # http://localhost:5173, backend expected at http://localhost:5074
```

Set `VITE_API_URL` to point at a different backend.

## How it fits together

| Path                          | What                                                                                                                |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| `src/app.css`                 | Tailwind import and the design tokens (`@theme`). No other CSS in the app.                                          |
| `src/lib/utils/baseline-sdk/` | Generated client. Do not edit; regenerate with `openapitools.json`. One hand patch, see below.                      |
| `src/lib/api/client.ts`       | The one fetch wrapper: sets `X-Role` / `X-Crew-Id` on every SDK call and unwraps `{ success, data, error }`.        |
| `src/lib/api/index.ts`        | Typed functions for every backend call (`api.mission()`, `api.submitMeasurement()`, …).                             |
| `src/lib/api/types.ts`        | Response shapes. The SDK types many numbers as `{}`, so screens use these.                                          |
| `src/lib/stores/`             | `session` (role + crew), `mission` (2 s poll, `nowMs()` mission time), `flow` (result, alert, protocol run, timer). |
| `src/lib/metrics.ts`          | Entry controls per metric, steppers and quick picks, source line under a status reason.                             |
| `src/lib/chest.ts`            | Chest figure levels: heart = worst of HR / BP, lungs = SpO2 (no new rules).                                         |
| `src/lib/demo.ts`             | Presenter-only helpers (chest pain example). Not used by crew or ground screens.                                    |
| `src/lib/components/`         | `Button`, `StatusBadge`, `Icon`, `AlertBanner`, `OfflineBanner`, `ErrorBox`, `CrewBar`, `PacketBoxes`,              |
|                               | `TrendChart`, `ChestFigure`, `PresenterBar`, `entry/` (NumberStepper, RangeSlider, QuickPick).                      |
| `src/routes/`                 | `/` role picker · `/crew/*` home, measure, status, protocol, packet, trends · `/ground/*` overview, inbox, reports. |

## Rules baked in

- Shown level is `context?.level ?? status.level`; a lowered level always says "Lowered from … by …".
- Status is never colour alone: word + shape (circle, triangle, octagon) + colour.
- Mission time comes from `GET /api/mission`, never `Date.now()`.
- No CDN at runtime: fonts come from `@fontsource/*`, ECharts from npm.
- Steppers stay inside the plausible range from config; typed values do not, so 422 "Save anyway" still works.

## Presenter bar

The striped bar under every crew and ground screen is for the demo presenter, not the crew app. It
can fast-forward or jump the mission clock, run Scene 5 (real time, link up, 12 min delay), load a
chest pain example for Alex Rivera (synthetic), cut or restore the link, and reset the demo
(reseed: data and clock back to day 0). Each button explains itself; the result appears under the
buttons.

## Checks before pushing

```sh
npm run check        # svelte-check
npm run lint         # prettier --check + eslint (the generated SDK is excluded)
npm run build
```

## Hand patch in the generated SDK

`models/AlertDto.ts`: the backend OpenAPI schema leaves alert `context` untyped, so the generator
calls `FromJSON` / `ToJSON` helpers that do not exist. The two lines pass the object through and are
marked in the file. Redo them after regenerating, or give `context` a schema in the backend.
