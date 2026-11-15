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
