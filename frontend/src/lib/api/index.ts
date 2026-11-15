import { sdk, unwrap } from './client.ts';
import type {
	Alert,
	Baseline,
	CrewMember,
	HowTo,
	LinkMessage,
	MeasurementResponse,
	Metric,
	Mission,
	OverviewRow,
	Packet,
	Protocol,
	ProtocolRun,
	Reading,
	Reminder,
	Report,
	ReportPeriod,
	SymptomResponse
} from './types.ts';

export { ApiException, errorMessage } from './client.ts';
export * from './types.ts';

const need = (id?: string | null) => {
	if (!id) throw new Error('No crew member selected.');
	return id;
};

let metricsCache: Promise<Metric[]> | null = null;

/** Every backend call the screens make, as plain typed functions. */
export const api = {
	mission: () => unwrap<Mission>(sdk.mission.apiMissionGetRaw()),

	metrics: () => (metricsCache ??= unwrap<Metric[]>(sdk.config.apiConfigMetricsGetRaw())),
	howto: (code: string) =>
		unwrap<HowTo>(sdk.config.apiConfigHowtoMetricCodeGetRaw({ metricCode: code })),
	protocol: (id: string) => unwrap<Protocol>(sdk.config.apiConfigProtocolsIdGetRaw({ id })),

	crew: () => unwrap<CrewMember[]>(sdk.crew.apiCrewGetRaw()),
	reminders: (id?: string | null) =>
		unwrap<Reminder[]>(sdk.crew.apiCrewCrewIdRemindersGetRaw({ crewId: need(id), openOnly: true })),
	measurements: (id: string | null | undefined, metric: string, days?: number) =>
		unwrap<Reading[]>(
			sdk.crew.apiCrewCrewIdMeasurementsGetRaw({ crewId: need(id), metric, days: days as never })
		),
	baselines: (id?: string | null) =>
		unwrap<Baseline[]>(sdk.crew.apiCrewCrewIdBaselinesGetRaw({ crewId: need(id) })),
	alerts: (id?: string | null) =>
		unwrap<Alert[]>(sdk.crew.apiCrewCrewIdAlertsGetRaw({ crewId: need(id) })),

	submitMeasurement: (
		id: string | null | undefined,
		metricCode: string,
		value: number,
		confirmImplausible = false
	) =>
		unwrap<MeasurementResponse>(
			sdk.crew.apiCrewCrewIdMeasurementsPostRaw({
				crewId: need(id),
				submitMeasurementRequest: { metricCode, value: value as never, confirmImplausible }
			})
		),
	logSymptom: (id: string | null | undefined, symptomCode: string, note?: string) =>
		unwrap<SymptomResponse>(
			sdk.crew.apiCrewCrewIdSymptomsPostRaw({
				crewId: need(id),
				logSymptomRequest: { symptomCode, note: note ?? null }
			})
		),

	startRun: (id: string | null | undefined, protocolId: string, alertId: string | null) =>
		unwrap<ProtocolRun>(
			sdk.crew.apiCrewCrewIdProtocolRunsPostRaw({
				crewId: need(id),
				startProtocolRequest: { protocolId, alertId }
			})
		),
	answerStep: (runId: string, nodeId: string, answer: string | null) =>
		unwrap<ProtocolRun>(
			sdk.crew.apiCrewProtocolRunsRunIdStepsPostRaw({
				runId,
				protocolStepRequest: { nodeId, answer }
			})
		),
	endRun: (runId: string, outcome: string | null) =>
		unwrap<ProtocolRun>(
			sdk.crew.apiCrewProtocolRunsRunIdEndPostRaw({ runId, endProtocolRequest: { outcome } })
		),

	packetDraft: (id: string | null | undefined, alertId?: string | null, runId?: string | null) =>
		unwrap<Packet>(
			sdk.crew.apiCrewCrewIdPacketsDraftGetRaw({
				crewId: need(id),
				alertId: alertId ?? undefined,
				runId: runId ?? undefined
			})
		),
	sendPacket: (
		id: string | null | undefined,
		alertId: string | null,
		runId: string | null,
		note: string
	) =>
		unwrap<LinkMessage>(
			sdk.crew.apiCrewCrewIdPacketsPostRaw({
				crewId: need(id),
				queuePacketRequest: { alertId, runId, note: note || null }
			})
		),

	overview: () => unwrap<OverviewRow[]>(sdk.ground.apiGroundOverviewGetRaw()),
	inbox: (type = 'Packet,Alert') =>
		unwrap<LinkMessage[]>(sdk.ground.apiGroundInboxGetRaw({ type })),

	reportPeriods: (window: string) =>
		unwrap<ReportPeriod[]>(sdk.reports.apiReportsPeriodsGetRaw({ window })),
	memberReport: (id: string, window: string, period: number) =>
		unwrap<Report>(
			sdk.reports.apiCrewCrewIdReportsWindowPeriodGetRaw({
				crewId: id,
				window,
				period: String(period)
			})
		),
	crewReport: (window: string, period: number) =>
		unwrap<Report>(
			sdk.reports.apiReportsCrewWindowPeriodGetRaw({ window, period: String(period) })
		),

	/** Presenter controls (open endpoints, no role needed). Each returns the new mission state. */
	demo: {
		clock: (opts: { fastForward?: boolean; advanceSeconds?: number }) =>
			unwrap<Mission>(
				sdk.demo.apiDemoClockPostRaw({
					clockRequest: {
						rate: null,
						paused: null,
						fastForward: opts.fastForward ?? null,
						advanceSeconds: (opts.advanceSeconds ?? null) as never
					}
				})
			),
		link: (up: boolean) =>
			unwrap<Mission>(
				sdk.demo.apiDemoLinkPostRaw({ linkRequest: { up, oneWayLatencySeconds: null } })
			),
		scene5: () => unwrap<Mission>(sdk.demo.apiDemoScene5PostRaw()),
		reseed: () => unwrap<unknown>(sdk.demo.apiDemoReseedPostRaw())
	}
};
