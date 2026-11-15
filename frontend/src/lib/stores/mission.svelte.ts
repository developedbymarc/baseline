import { api } from '#lib/api/index.ts';
import type { Mission } from '#lib/api/types.ts';

/** Mission clock and link state. Always read from the API, never from Date.now(). */
class MissionStore {
	state = $state<Mission | null>(null);
	offline = $state(false);

	async refresh() {
		try {
			this.state = await api.mission();
			this.offline = false;
		} catch {
			this.offline = true;
		}
	}

	/** Polls every 2 s. Returns a cleanup function, so it can be returned from `$effect`. */
	poll() {
		this.refresh();
		const timer = setInterval(() => this.refresh(), 2000);
		return () => clearInterval(timer);
	}
}

export const mission = new MissionStore();
