# BASELINE frontend

SvelteKit 5 + Tailwind CSS 4 + ECharts. Crew screens (tablet) and ground screens (desktop) in the
Option A "Flight Deck" style from `ui-spec.md`. All data is synthetic; decision support, not diagnosis.

```sh
npm install
npm run dev          # http://localhost:5173, backend expected at http://localhost:5074
```

Set `VITE_API_URL` to point at a different backend.

## How it fits together

| Path | What |
|---|---|
| `src/app.css` | Tailwind import and the design tokens (`@theme`). No other CSS in the app. |
| `src/lib/utils/baseline-sdk/` | Generated client. Do not edit; regenerate with `openapitools.json`. |
| `src/lib/api/client.ts` | The one fetch wrapper: sets `X-Role` / `X-Crew-Id` on every SDK call and unwraps `{ success, data, error }`. |
| `src/lib/api/index.ts` | Typed functions for every backend call (`api.mission()`, `api.submitMeasurement()`, …). |
| `src/lib/api/types.ts` | Response shapes. The SDK types many numbers as `{}`, so screens use these. |
| `src/lib/stores/` | `session` (role + crew), `mission` (2 s poll), `flow` (result, alert, protocol run). |
| `src/lib/components/` | `Button`, `StatusBadge`, `Icon`, `AlertBanner`, `CrewBar`, `PacketBoxes`, `TrendChart`. |
| `src/routes/` | `/` role picker · `/crew/*` home, measure, status, protocol, packet, trends · `/ground/*` overview, inbox, reports. |

## Rules baked in

- Shown level is `context?.level ?? status.level`; a lowered level always says "Lowered from … by …".
- Status is never colour alone: word + shape (circle, triangle, octagon) + colour.
- Mission time comes from `GET /api/mission`, never `Date.now()`.
- No CDN at runtime: fonts come from `@fontsource/*`, ECharts from npm.
