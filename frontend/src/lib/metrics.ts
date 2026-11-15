import type { Metric } from '#lib/api/types.ts';

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
	mood: 'Mood'
};

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

export const isMood = (code: string) => code.startsWith('mood');

/** Start point for the first stepper press: the middle of the normal band, from config. */
export function startValue(m: Metric) {
	return m.normalLow != null && m.normalHigh != null
		? (m.normalLow + m.normalHigh) / 2
		: m.plausibleMin;
}

export const roundTo = (n: number, step: number) => Math.round(n / step) * step;
