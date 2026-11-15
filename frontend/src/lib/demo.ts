/**
 * Presenter-only helpers (not part of the crew app). Every call goes through the SDK.
 */
import { sdk, unwrap } from '#lib/api/client.ts';
import type { CrewMember, MeasurementResponse, SymptomResponse } from '#lib/api/types.ts';
import type { InitOverrideFunction } from '#lib/utils/baseline-sdk/index.ts';

/** Per-call identity, kept by the fetch wrapper; the generated headers (Content-Type) stay. */
const as =
	(role: 'ground' | 'crew', crewId?: string): InitOverrideFunction =>
	async ({ init }) => ({
		headers: {
			...(init.headers as Record<string, string>),
			'X-Role': role,
			...(crewId ? { 'X-Crew-Id': crewId } : {})
		}
	});

/** The crew list as ground sees it (the crew role only sees itself). */
export const crewList = () => unwrap<CrewMember[]>(sdk.crew.apiCrewGetRaw(as('ground')));

/**
 * Demo readings for the chest pain example: synthetic values chosen to be ACT NOW against the
 * limits in config (they are example data, not limits; the status still comes from the backend).
 */
export const EXAMPLE_READINGS: [string, number][] = [
	['hr_rest', 130],
	['bp_sys', 145],
	['bp_dia', 92],
	['spo2', 92]
];

/**
 * "Chest pain example": real time with the link up (scene 5), then Alex Rivera (synthetic) logs
 * chest pain and the red readings above, exactly as the crew app would.
 */
export async function chestPainExample() {
	const crew = await crewList();
	const member = crew.find((c) => c.name.startsWith('Alex')) ?? crew[0];
	if (!member) throw new Error('No crew member to use for the example.');
	await unwrap(sdk.demo.apiDemoScene5PostRaw());
	const crewHeaders = as('crew', member.id);
	await unwrap<SymptomResponse>(
		sdk.crew.apiCrewCrewIdSymptomsPostRaw(
			{ crewId: member.id, logSymptomRequest: { symptomCode: 'chest_pain', note: 'pressure behind sternum' } },
			crewHeaders
		)
	);
	const levels: string[] = [];
	for (const [metricCode, value] of EXAMPLE_READINGS) {
		const r = await unwrap<MeasurementResponse>(
			sdk.crew.apiCrewCrewIdMeasurementsPostRaw(
				{ crewId: member.id, submitMeasurementRequest: { metricCode, value: value as never, confirmImplausible: false } },
				crewHeaders
			)
		);
		levels.push(String(r.status.level));
	}
	return { member, levels };
}
