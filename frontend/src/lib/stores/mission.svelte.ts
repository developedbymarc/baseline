import { api } from '#lib/api/index.ts';
import type { Mission } from '#lib/api/types.ts';

/** Mission clock and link state. Always read from the API, never from Date.now(). */
class MissionStore {
	state = $state<Mission | null>(null);
	offline = $state(false);
	/** performance.now() when `state` arrived, to move mission time on between polls. */
	#fetchedAt = 0;

	async refresh() {
		try {
			this.state = await api.mission();
			this.#fetchedAt = performance.now();
			this.offline = false;
		} catch {
			this.offline = true;
		}
	}

	/**
	 * Current mission time in ms: the last `missionNow` from the API, moved on by the time
	 * since that poll at the mission clock rate (fast-forward included). Null before the first poll.
	 */
	nowMs(): number | null {
		const s = this.state;
		if (!s) return null;
		const base = Date.parse(s.missionNow);
		if (s.clockPaused) return base;
		return base + (performance.now() - this.#fetchedAt) * (s.clockRate ?? 1);
	}

	/** Polls every 2 s. Returns a cleanup function, so it can be returned from `$effect`. */
	poll() {
		this.refresh();
		const timer = setInterval(() => this.refresh(), 2000);
		return () => clearInterval(timer);
	}
}

export const mission = new MissionStore();
