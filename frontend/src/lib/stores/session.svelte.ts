import { browser } from '$app/environment';
import type { CrewMember, Role } from '#lib/api/types.ts';

const KEY = 'baseline.session';

/** Mock auth: the role picker decides X-Role and X-Crew-Id for every request. */
class Session {
	role = $state<Role | null>(null);
	crewId = $state<string | null>(null);
	crewName = $state<string | null>(null);

	constructor() {
		if (!browser) return;
		try {
			Object.assign(this, JSON.parse(sessionStorage.getItem(KEY) ?? '{}'));
		} catch {
			/* ignore a corrupt value */
		}
	}

	#save() {
		if (browser) {
			sessionStorage.setItem(
				KEY,
				JSON.stringify({ role: this.role, crewId: this.crewId, crewName: this.crewName })
			);
		}
	}

	signIn(role: Role, crew?: CrewMember) {
		this.role = role;
		this.crewId = crew?.id ?? null;
		this.crewName = crew?.name ?? null;
		this.#save();
	}

	signOut() {
		this.role = null;
		this.crewId = null;
		this.crewName = null;
		if (browser) sessionStorage.removeItem(KEY);
	}
}

export const session = new Session();
