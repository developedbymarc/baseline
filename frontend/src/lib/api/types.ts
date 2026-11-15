/**
 * App-level shapes for what the backend returns inside the `{ success, data, error }` envelope.
 * The generated SDK types many numeric fields as `{}` and several endpoints as `object`,
 * so the screens use these instead. Fields marked "assumed" are not in the SDK; they follow
 * ui-spec.md and should be checked against docs/API.md.
 */

export type Level = 'Green' | 'Yellow' | 'Red';
export type Role = 'Ground' | 'Crew';
/** The API may send a level as a name or as an enum number; `toLevel` normalises it. */
export type RawLevel = Level | number | string;

export interface Source {
	id?: string;
	publisher?: string | null;
	title?: string | null;
	url?: string | null;
}

export interface Mission {
	name: string;
	launchDate: string;
	missionNow: string;
	met: string;
	clockRate: number;
	clockPaused: boolean;
	linkUp: boolean;
	oneWayLatencySeconds: number;
	onboardFirst: boolean;
}

export interface CrewMember {
	id: string;
	name: string;
	role: string;
	age: number;
	sex: string;
}

export interface Metric {
	code: string;
	name: string;
	unit: string;
	plausibleMin: number;
	plausibleMax: number;
	normalLow: number | null;
	normalHigh: number | null;
	zWarn: number | null;
	hasStatus: boolean;
}

export interface HowTo {
	metricCode: string;
	available: boolean;
	markdown: string;
}

export interface ContextFactor {
	ruleId: string;
	message: string;
	severityModifier: number;
	sources: Source[];
}

export interface ContextInfo {
	level: RawLevel;
	originalLevel: RawLevel;
	downgraded: boolean;
	downgradedBy: string[];
	factors: ContextFactor[];
}

/** Assumed: `status` of a measurement result. */
export interface StatusInfo {
	level: RawLevel;
	reason?: string | null;
	rule?: string | null;
	baselineN?: number | null;
}

export interface Alert {
	id: string;
	level: RawLevel;
	reason: string;
	ruleId?: string;
	acknowledgedBy?: string | null;
}

/** Assumed: one point from GET /crew/{id}/measurements, and one ground overview cell. */
export interface Reading {
	metricCode: string;
	value: number;
	unit?: string;
	recordedAt: string;
	recordedMet?: string;
	level?: RawLevel | null;
	rule?: string | null;
	status?: StatusInfo | null;
	context?: ContextInfo | null;
}

/** Response of POST /crew/{id}/measurements. `alertId` is set for every yellow or red in-flight reading. */
export interface MeasurementResponse {
	status: StatusInfo;
	context?: ContextInfo | null;
	alertId?: string | null;
	protocolId?: string | null;
	packetDraft?: Packet | null;
}

/** Response of POST /crew/{id}/symptoms. It carries no reason text. */
export interface SymptomResponse {
	symptomLogId: string;
	alertId: string | null;
	protocolId: string | null;
}

export interface Baseline {
	metricCode: string;
	mean: number;
	sdEffective: number;
	n: number;
}

export interface Reminder {
	id?: string;
	metricCode: string;
	dueMet?: string | number | null;
	overdue: boolean;
}

export interface ProtocolOption {
	label: string;
	next?: string | null;
}

export interface ProtocolNode {
	id: string;
	type: 'question' | 'action' | 'measure' | 'end' | string;
	text: string;
	options?: ProtocolOption[];
	next?: string | null;
	metrics?: string[];
	src?: string[];
	outcome?: string | null;
	timerS?: number | null;
	buildPacket?: boolean;
	notifyGround?: boolean;
	review?: boolean;
}

export interface Protocol {
	id: string;
	title: string;
	version: string;
	start: string;
	disclaimer?: string | null;
	treeYaml: ProtocolNode[];
	sources: Source[];
}

export interface ProtocolRun {
	id: string;
	protocolId: string;
	alertId: string | null;
	steps?: unknown[] | null;
}

export interface PacketData {
	mission: { missionDay: number; inAdaptationWindow: boolean; oneWayLatencyText: string };
	crew: { name: string; role: string; age: number; sex: string };
	trigger: {
		summary: string;
		symptoms: { symptomCode: string; note: string | null; loggedMet: string }[];
	};
	measurements72h: {
		metricName: string;
		unit: string;
		value: number;
		recordedMet: string;
		level: RawLevel | null;
	}[];
	baselines: { summary: string }[];
	protocol: {
		title: string;
		version: string;
		steps: { nodeId: string; text: string; answer: string | null }[];
	} | null;
	contextFactors: ContextFactor[];
	followUp: { key: string; question: string; answer: string; filledFrom: string }[];
	recommendation: string;
}

export interface Packet {
	createdMet: string;
	crewName: string;
	alertId: string | null;
	runId: string | null;
	data: PacketData;
	text: string;
}

export interface LinkMessage {
	id: string;
	type: number | string;
	state: string;
	sentAt: string;
	deliveredAt: string | null;
	ageOfData: string;
	secondsUntilDelivery: number | null;
	payload: (Partial<Packet> & { reason?: string }) | null;
}

/** One cell of GET /ground/overview. `value`, `recordedAt` and `status` are null until a reading is delivered. */
export interface OverviewCell {
	metricCode: string;
	value: number | null;
	recordedAt: string | null;
	status: StatusInfo | null;
	context?: ContextInfo | null;
}
export interface OverviewRow {
	crewMemberId: string;
	name: string;
	role: string;
	cells: OverviewCell[];
}

export interface ReportPeriod {
	number: number;
	startDay: number;
	endDay: number;
	complete: boolean;
}

export interface MetricRow {
	metricCode: string;
	metricName: string;
	unit: string;
	windowN: number;
	windowMean: number | null;
	baselineMean: number | null;
	baselineN: number | null;
	delta: number | null;
	zSd: number | null;
	trend: string;
	missed: number;
	expected: number;
	lowConfidence: boolean;
}

export interface AlertCounts {
	measurementAlerts: number;
	symptomAlerts: number;
	withContextFactors: number;
	unexplainedAlertIds: string[];
}

export interface Report {
	dataAsOfMet: string;
	adaptationWindow: { overlaps: boolean; note: string | null };
	highlights: string[];
	alertsTotal: AlertCounts;
	members: {
		crewMemberId: string;
		name: string;
		role: string;
		rows: MetricRow[];
		missedTotal: number;
		highlights: string[];
	}[];
}
