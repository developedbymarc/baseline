import { session } from '#lib/stores/session.svelte.ts';
import {
	BASE_PATH,
	ConfigApi,
	Configuration,
	CrewApi,
	FetchError,
	GroundApi,
	MissionApi,
	ReportsApi,
	ResponseError,
	type ApiResponse,
	type Middleware
} from '#lib/utils/baseline-sdk/index.ts';

/** Thrown for every failed call, so screens only handle one error type. */
export class ApiException extends Error {
	constructor(
		public code: string,
		message: string,
		public status = 0
	) {
		super(message);
	}
}

/** Adds X-Role and X-Crew-Id to every request, read from the current session. */
const identity: Middleware = {
	async pre({ url, init }) {
		const headers = { ...(init.headers as Record<string, string>) };
		if (session.role) headers['X-Role'] = session.role;
		if (session.role === 'Crew' && session.crewId) headers['X-Crew-Id'] = session.crewId;
		return { url, init: { ...init, headers } };
	}
};

const config = new Configuration({
	basePath: import.meta.env.VITE_API_URL ?? BASE_PATH,
	middleware: [identity]
});

export const sdk = {
	config: new ConfigApi(config),
	crew: new CrewApi(config),
	ground: new GroundApi(config),
	mission: new MissionApi(config),
	reports: new ReportsApi(config)
};

interface Envelope<T> {
	success: boolean;
	data: T;
	error: { code: string; message: string } | null;
}

/**
 * Runs an SDK `*Raw` call and unwraps the `{ success, data, error }` envelope.
 * Raw is used on purpose: it skips the SDK's lossy model conversion and also works
 * for endpoints the generator typed as `void`.
 */
export async function unwrap<T>(call: Promise<ApiResponse<unknown>>): Promise<T> {
	try {
		const body = (await (await call).raw.json()) as Envelope<T>;
		if (!body.success) {
			throw new ApiException(body.error?.code ?? 'ERROR', body.error?.message ?? 'Request failed');
		}
		return body.data;
	} catch (e) {
		if (e instanceof ApiException) throw e;
		if (e instanceof ResponseError) {
			const body = (await e.response.json().catch(() => null)) as Envelope<unknown> | null;
			throw new ApiException(
				body?.error?.code ?? `HTTP_${e.response.status}`,
				body?.error?.message ?? 'The backend rejected this request.',
				e.response.status
			);
		}
		if (e instanceof FetchError || e instanceof TypeError) {
			throw new ApiException('NETWORK', 'Cannot reach the backend. Check that it is running.');
		}
		throw e;
	}
}

export const errorMessage = (e: unknown) =>
	e instanceof Error ? e.message : 'Something went wrong.';
