<script lang="ts">
	import { api, errorMessage } from '#lib/api/index.ts';
	import { chestPainExample } from '#lib/demo.ts';
	import { latencyText } from '#lib/format.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';

	type Props = {
		/** Called after every action so the open screen refetches through the SDK. */
		onrefresh: () => void;
		/** Called after "Reset demo" instead of onrefresh (crew ids change on reseed). */
		onreset: () => void | Promise<void>;
	};
	let { onrefresh, onreset }: Props = $props();

	const KEY = 'baseline.presenterOpen';
	const browser = typeof localStorage !== 'undefined'; // the app is client-only (ssr = false)
	let open = $state(browser && localStorage.getItem(KEY) === '1');
	let busy = $state(false);
	let confirmReset = $state(false);
	let error = $state('');
	/** What the last action did and what to do next, in plain words. */
	let notice = $state('');

	const m = $derived(mission.state);
	// Fast-forward is 1 mission day per 5 s; real time is rate 1.
	const fastForward = $derived((m?.clockRate ?? 1) > 1);
	const delay = $derived(m ? latencyText(m.oneWayLatencySeconds) : '12 min');

	function toggle() {
		open = !open;
		if (browser) localStorage.setItem(KEY, open ? '1' : '0');
	}

	/** Runs an action, refreshes mission state and the open screen, then shows `message`. */
	async function run(action: () => Promise<unknown>, message: (result: unknown) => string, after: () => void | Promise<void> = onrefresh) {
		busy = true;
		error = '';
		notice = '';
		try {
			const result = await action();
			await mission.refresh();
			await after();
			notice = message(result);
			// With fast-forward on, delayed messages land a moment later (12 min = 0.04 s): refresh
			// once more so e.g. the ground inbox shows the packet without waiting for its 5 s poll.
			if (after === onrefresh && (mission.state?.clockRate ?? 1) > 1) setTimeout(onrefresh, 1500);
		} catch (e) {
			error = errorMessage(e);
		} finally {
			busy = false;
			confirmReset = false;
		}
	}

	const met = () => (mission.state ? `Mission time is now ${mission.state.met}.` : '');

	const JUMPS = [
		{ label: '+1 day', seconds: 86400, words: '1 day' },
		{ label: '+1 week', seconds: 604800, words: '1 week' },
		{ label: '+3 weeks', seconds: 1814400, words: '3 weeks' }
	];
	const btn =
		'flex min-h-12 flex-col items-start justify-center rounded-lg border border-muted/50 bg-bg px-3 py-1 text-left text-sm font-medium text-ink transition hover:bg-raised active:scale-[0.97] disabled:opacity-50';
	const cap = 'text-xs font-normal text-muted';
</script>

<!-- In the page flow under the footer, never fixed over the crew or ground UI. -->
<aside
	class="border-t-2 border-dashed border-muted/60 bg-[repeating-linear-gradient(135deg,#0a0f1c_0_12px,#0e1424_12px_24px)] px-6 py-2 print:hidden"
	aria-label="Presenter demo controls"
>
	<div class="flex flex-wrap items-center gap-3">
		<button
			type="button"
			class="min-h-11 rounded-lg border border-muted/50 bg-bg px-3 text-sm font-medium text-ink hover:bg-raised"
			aria-expanded={open}
			aria-controls="presenter-controls"
			onclick={toggle}
		>
			{open ? '▾' : '▸'} PRESENTER
		</button>
		<span class="text-xs tracking-wider text-muted uppercase">demo controls, not part of the crew app</span>
		<span class="ml-auto font-mono text-sm text-muted tabular-nums">
			{#if m}MET {m.met} · ×{Math.round(m.clockRate)} {fastForward ? '· fast-forward' : '· real time'} · {m.linkUp ? 'link up' : 'link down'}{:else}no mission state{/if}
		</span>
	</div>

	{#if open}
		<div id="presenter-controls" class="space-y-2 pt-2 pb-1">
			<div class="flex flex-wrap items-stretch gap-2">
				<button
					type="button"
					class="{btn} {fastForward ? 'border-watch text-watch' : ''}"
					aria-pressed={fastForward}
					disabled={busy}
					onclick={() => {
						const on = !fastForward; // decided before the refresh changes the state
						run(
							() => api.demo.clock({ fastForward: on }),
							() =>
								on
									? 'Fast-forward on: 1 mission day passes every 5 s, so the stored mission history appears and delayed messages arrive almost at once. Turn it off before a live scene.'
									: `Real time again: 1 mission second per second. ${met()}`
						);
					}}
				>
					Fast-forward: {fastForward ? 'on' : 'off'}
					<span class={cap}>{fastForward ? 'tap for real time' : '1 mission day every 5 s'}</span>
				</button>
				{#each JUMPS as j (j.seconds)}
					<button
						type="button"
						class={btn}
						disabled={busy}
						onclick={() =>
							run(
								() => api.demo.clock({ advanceSeconds: j.seconds }),
								() => `Jumped ${j.words} ahead. ${met()} Readings stored for those days are now visible.`
							)}
					>
						{j.label}
						<span class={cap}>jump the clock</span>
					</button>
				{/each}

				<span class="mx-1 w-px self-stretch bg-line" aria-hidden="true"></span>

				<button
					type="button"
					class={btn}
					disabled={busy}
					onclick={() =>
						run(
							() => api.demo.scene5(),
							() =>
								`Scene 5 ready: real time, link up, ${delay} one-way. Nothing is logged yet. Next, as the crew member: tap "Report a symptom", then enter heart rate 130 to open the chest pain protocol.`
						)}
				>
					Scene 5 (chest pain)
					<span class={cap}>real time · link up · {delay} delay</span>
				</button>
				<button
					type="button"
					class={btn}
					disabled={busy}
					onclick={() =>
						run(chestPainExample, (r) => {
							const name = (r as Awaited<ReturnType<typeof chestPainExample>>).member.name;
							return `Chest pain example: ${name} logged chest pain with heart rate 130, BP 145/92 and SpO2 92 (ACT NOW). Clock in real time. Sign in as this crew member to see the red banner and chest figure; ground sees it after the ${delay} delay. Fast-forwarding later shows newer normal readings again.`;
						})}
				>
					Chest pain example
					<span class={cap}>Alex: chest pain + red HR, BP, SpO2</span>
				</button>
				<button
					type="button"
					class="{btn} {m && !m.linkUp ? 'border-watch text-watch' : ''}"
					aria-pressed={m ? !m.linkUp : undefined}
					disabled={busy || !m}
					onclick={() => {
						const up = !m!.linkUp;
						run(
							() => api.demo.link(up),
							() =>
								up
									? `Link restored: held messages leave now and reach ground ${delay} later.`
									: 'Link cut: crew messages are held on board until the link returns. The crew side keeps working.'
						);
					}}
				>
					Link: {m?.linkUp === false ? 'down' : 'up'}
					<span class={cap}>{m?.linkUp === false ? 'tap to restore' : 'tap to cut the radio link'}</span>
				</button>

				<span class="mx-1 w-px self-stretch bg-line" aria-hidden="true"></span>

				{#if confirmReset}
					<span class="self-center text-sm text-act-ink">Reload all demo data and set the clock back to day 0? Everything entered is lost.</span>
					<button
						type="button"
						class="{btn} border-act text-act-ink"
						disabled={busy}
						onclick={() =>
							run(
								() => api.demo.reseed(),
								() => `Demo reset: data reloaded, clock back to the start (${mission.state?.met ?? 'day 0'}), real time, link up.`,
								onreset
							)}
					>
						Yes, reset
						<span class={cap}>cannot be undone</span>
					</button>
					<button type="button" class={btn} disabled={busy} onclick={() => (confirmReset = false)}>Cancel</button>
				{:else}
					<button type="button" class={btn} disabled={busy} onclick={() => (confirmReset = true)}>
						Reset demo…
						<span class={cap}>reload data · clock back to day 0</span>
					</button>
				{/if}
			</div>

			<p class="min-h-5 text-sm" aria-live="polite">
				{#if busy}<span class="text-muted">Working…</span>
				{:else if error}<span class="text-act-ink">{error}</span>
				{:else if notice}<span class="text-ink">{notice}</span>
				{:else}<span class="text-muted">Each button says what it does. Results appear here.</span>{/if}
			</p>
		</div>
	{/if}
</aside>
