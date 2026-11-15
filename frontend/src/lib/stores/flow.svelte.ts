import { browser } from '$app/environment';
import type {
	MeasurementResponse, Metric, Packet, Protocol, ProtocolRun
} from '#lib/api/types.ts';

export interface Outcome {
	metric: Metric;
	value: number;
	response: MeasurementResponse;
}

export interface OpenAlert {
	alertId: string | null;
	reason: string;
	protocolId: string | null;
}

const CLOSED = 'baseline.closedAlerts';

/** State that has to survive moving between crew screens (result, alert, protocol run). */
class Flow {
	outcomes = $state<Outcome[]>([]);
	/** Where "Done" goes after a measurement. Set when entry was opened from the protocol runner. */
	returnTo = $state<string | null>(null);
	alert = $state<OpenAlert | null>(null);
	protocol = $state<Protocol | null>(null);
	run = $state<ProtocolRun | null>(null);
	nodeId = $state<string | null>(null);
	/** 1-based count of protocol steps shown, for the header. */
	step = $state(1);
	packet = $state<Packet | null>(null);

	#closed(): string[] {
		if (!browser) return [];
		try {
			return JSON.parse(sessionStorage.getItem(CLOSED) ?? '[]');
		} catch {
			return [];
		}
	}

	isClosed(alertId: string) {
		return this.#closed().includes(alertId);
	}

	openAlert(alert: OpenAlert) {
		if (alert.alertId && this.isClosed(alert.alertId)) return;
		this.alert = alert;
	}

	/** Called when the protocol run ends or the packet is sent. */
	closeAlert() {
		const id = this.alert?.alertId;
		if (id && browser) sessionStorage.setItem(CLOSED, JSON.stringify([...this.#closed(), id]));
		this.alert = null;
		this.run = null;
		this.protocol = null;
		this.nodeId = null;
		this.step = 1;
		this.packet = null;
	}
}

export const flow = new Flow();
