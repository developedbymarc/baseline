import type { ContextInfo, Level, RawLevel } from '#lib/api/types.ts';

export const LEVEL_WORD: Record<Level, string> = { Green: 'OK', Yellow: 'WATCH', Red: 'ACT NOW' };
const ORDER: Level[] = ['Green', 'Yellow', 'Red'];

/** Normalises an API level. Numbers are assumed to be 0 = Green, 1 = Yellow, 2 = Red. */
export function toLevel(value: RawLevel | null | undefined): Level | null {
	if (value == null) return null;
	if (typeof value === 'number') return ORDER[value] ?? null;
	return ORDER.find((l) => l.toLowerCase() === value.toLowerCase()) ?? null;
}

export const worst = (levels: (Level | null)[]): Level | null =>
	levels.reduce<Level | null>(
		(a, b) => (b && (!a || ORDER.indexOf(b) > ORDER.indexOf(a)) ? b : a),
		null
	);

/** Shown level = context.level ?? status.level (ui-spec §2). */
export const shownLevel = (r: {
	context?: ContextInfo | null;
	status?: { level: RawLevel } | null;
	level?: RawLevel | null;
}) => toLevel(r.context?.level) ?? toLevel(r.status?.level) ?? toLevel(r.level);

const RULE_LABELS: Record<string, string> = {
	short_sleep: 'Short sleep',
	high_stress: 'Stress',
	recent_fever: 'Recent fever',
	adaptation_window: 'Adaptation window',
	missed_exercise: 'Missed exercise',
	space_weather_kp: 'Geomagnetic storm',
	space_weather_spe: 'Solar particle event',
	elevated_co2: 'Cabin CO2'
};
export const ruleLabel = (id: string) =>
	RULE_LABELS[id] ?? id.replaceAll('_', ' ').replace(/^./, (c) => c.toUpperCase());

export const latencyText = (seconds: number) =>
	seconds < 90 ? `${Math.round(seconds)} s` : `${Math.round(seconds / 60)} min`;

const pad = (n: number) => String(Math.floor(n)).padStart(2, '0');

/** Mission elapsed time from seconds, e.g. 0d 00:35:00. Strings pass through unchanged. */
export function formatMet(value: string | number | null | undefined) {
	if (value == null) return '';
	if (typeof value === 'string') return value;
	const abs = Math.abs(value);
	const text = `${Math.floor(abs / 86400)}d ${pad((abs % 86400) / 3600)}:${pad((abs % 3600) / 60)}:${pad(abs % 60)}`;
	return value < 0 ? `-${text}` : text;
}

export const metFromDates = (at: string, launch: string) =>
	formatMet((Date.parse(at) - Date.parse(launch)) / 1000);

/** "27 min ago", measured against mission time, not the wall clock. */
export function ageText(at: string, missionNow: string) {
	const min = Math.max(0, (Date.parse(missionNow) - Date.parse(at)) / 60000);
	if (min < 1) return 'just now';
	if (min < 90) return `${Math.round(min)} min ago`;
	if (min < 48 * 60) return `${Math.round(min / 60)} h ago`;
	return `${Math.round(min / 1440)} d ago`;
}

export const fmt = (n: number | null | undefined, digits = 1) =>
	n == null ? '–' : Number.isInteger(n) ? String(n) : n.toFixed(digits);

export const signed = (n: number | null | undefined, digits = 1) =>
	n == null ? '–' : `${n > 0 ? '+' : ''}${fmt(n, digits)}`;

/** Missions day from a MET string such as "1d 00:35:00" (day 1 = 0d). */
export const missionDay = (met: string) => {
	const d = /^(-?\d+)d/.exec(met)?.[1];
	return d == null ? null : Number(d) + 1;
};
