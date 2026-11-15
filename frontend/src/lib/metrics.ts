import type { Metric, StatusInfo } from '#lib/api/types.ts';
import { toLevel } from '#lib/format.ts';

export interface MetricGroup {
	id: string;
	label: string;
	metrics: Metric[];
}

const LABELS: Record<string, string> = {
	hr: 'Heart rate',
	bp: 'Blood pressure',
	temp: 'Temperature',
	spo2: 'SpO2',
	sleep: 'Sleep',
	mood: 'Mood',
	exercise: 'Exercise'
};

/** Tab icon per metric group (Icon.svelte names). */
export const GROUP_ICON: Record<
	string,
	'heart' | 'gauge' | 'thermometer' | 'droplet' | 'moon' | 'smile' | 'activity'
> = {
	hr: 'heart',
	bp: 'gauge',
	temp: 'thermometer',
	spo2: 'droplet',
	sleep: 'moon',
	mood: 'smile',
	exercise: 'activity'
};

/** Which entry control a metric gets (item 23). Unknown codes fall back to a number stepper. */
export type EntryKind = 'number' | 'slider' | 'sleep' | 'mood' | 'exercise';
export function entryKind(code: string): EntryKind {
	if (code.startsWith('spo2')) return 'slider';
	if (code.startsWith('sleep')) return 'sleep';
	if (code.startsWith('mood')) return 'mood';
	if (code.startsWith('exercise')) return 'exercise';
	return 'number';
}

/** Quick-pick values. UI choices for fewer taps, not health limits (those come from config). */
export const SLEEP_PICKS = [5, 6, 7, 8, 9];
export const EXERCISE_PICKS = [0, 15, 30, 45, 60];
export const EXERCISE_STEP = 5;
/** Mood words, 1 = worst, 5 = best (metrics.yaml: mood 1-5). */
export const MOOD_WORDS = ['Very low', 'Low', 'Okay', 'Good', 'Very good'];

/** Keeps stepper results inside the plausible range from config. Typed values are not clamped. */
export const clamp = (n: number, m: Metric) =>
	Math.min(m.plausibleMax, Math.max(m.plausibleMin, n));

/** 7.25 -> "7 h 15 min". */
export function formatHours(h: number) {
	const total = Math.round(h * 60);
	const hours = Math.floor(total / 60);
	const min = total % 60;
	return min ? `${hours} h ${min} min` : `${hours} h`;
}

export const parseNumber = (text: string | undefined) => {
	const n = Number((text ?? '').replace(',', '.'));
	return text?.trim() && Number.isFinite(n) ? n : null;
};

/** Next stepper value: from the start value when empty, else one step, rounded and clamped. */
export function stepped(
	m: Metric,
	text: string | undefined,
	direction: 1 | -1,
	step = stepFor(m.code)
) {
	const current = parseNumber(text);
	const next = current == null ? startValue(m) : clamp(current + direction * step, m);
	return String(Number(roundTo(next, step).toFixed(2)));
}

/** Entry chips: hr_rest -> "Heart rate", bp_sys + bp_dia -> one "Blood pressure" chip. */
export function groupMetrics(metrics: Metric[]): MetricGroup[] {
	const groups = new Map<string, MetricGroup>();
	for (const m of metrics) {
		const id = m.code.split('_')[0];
		const group = groups.get(id) ?? { id, label: LABELS[id] ?? m.name, metrics: [] };
		group.metrics.push(m);
		groups.set(id, group);
	}
	return [...groups.values()];
}

/** Stepper size per metric (ui-spec §3.2). */
export function stepFor(code: string) {
	if (code.startsWith('temp')) return 0.1;
	if (code.startsWith('sleep')) return 0.25;
	return 1;
}

/** Start point for the first stepper press: the middle of the normal band, from config. */
export function startValue(m: Metric) {
	return m.normalLow != null && m.normalHigh != null
		? (m.normalLow + m.normalHigh) / 2
		: m.plausibleMin;
}

export const roundTo = (n: number, step: number) => Math.round(n / step) * step;

/**
 * Source lines under a status reason (no new rules: only which config source backs the limit that
 * fired). Threshold part: the absolute level and direction pick the field (Red/high = crit_high,
 * Yellow/low = warn_low, …), then the source listing that field. The absolute level is used, not
 * status.level, because a reading can be Red by z-score while its threshold level is only Yellow.
 * Z-score part: the personal baseline sentence. Nothing for a green flag.
 */
export function statusSourceLines(metric: Metric, status: StatusInfo): string[] {
	const lines: string[] = [];
	if (toLevel(status.level) === 'Green' || toLevel(status.level) == null) return lines;
	const rule = status.rule ?? '';
	const absolute = toLevel(status.absoluteLevel);
	const direction = status.direction;
	// Not gated on `rule`: the API names only the deciding rule, so a reading that is Red by z-score
	// but over a Yellow threshold reports rule "zscore" although a threshold was crossed.
	if (absolute && absolute !== 'Green' && direction) {
		const field = `${absolute === 'Red' ? 'crit' : 'warn'}_${direction}`;
		const source = metric.sources?.find((s) => s.fields.includes(field));
		if (source) {
			lines.push(
				source.publisher.toLowerCase() === 'team'
					? 'Source: Team starting value, not clinically validated'
					: `Source: ${source.publisher}, ${source.title}`
			);
		}
	}
	if (rule.includes('zscore')) {
		lines.push(
			`Personal baseline${status.baselineN != null ? `, n=${status.baselineN}` : ''}; z limits are team starting values`
		);
	}
	return lines;
}
