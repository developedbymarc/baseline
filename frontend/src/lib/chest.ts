import { api, type Level, type Reading } from '#lib/api/index.ts';
import { shownLevel, worst } from '#lib/format.ts';

/**
 * Chest status figure (item 16). No new rules: it only redraws statuses the app already has.
 * Heart = worst shown level of the latest resting HR, systolic and diastolic BP.
 * Lungs = shown level of the latest SpO2.
 */
export const HEART_CODES = ['hr_rest', 'bp_sys', 'bp_dia'];
export const LUNG_CODES = ['spo2'];

export interface ChestLevels {
	heart: Level | null;
	lungs: Level | null;
}

export const chestLevels = (levels: Record<string, Level | null | undefined>): ChestLevels => ({
	heart: worst(HEART_CODES.map((c) => levels[c] ?? null)),
	lungs: worst(LUNG_CODES.map((c) => levels[c] ?? null))
});

export const newest = (points: Reading[]) =>
	[...points].sort((a, b) => Date.parse(a.recordedAt) - Date.parse(b.recordedAt)).at(-1);

/** Latest reading (last 7 days) per metric code, for the home tiles and the figure. */
export async function latestReadings(crewId: string | null | undefined, codes: string[]) {
	const entries = await Promise.all(
		codes.map(async (c) => [c, newest(await api.measurements(crewId, c, 7))] as const)
	);
	return Object.fromEntries(entries) as Record<string, Reading | undefined>;
}

export const levelsOf = (readings: Record<string, Reading | undefined>) =>
	Object.fromEntries(Object.entries(readings).map(([c, r]) => [c, r ? shownLevel(r) : null])) as Record<
		string,
		Level | null
	>;
