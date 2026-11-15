<script lang="ts">
	import { api, errorMessage } from '#lib/api/index.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';

	type Props = {
		/** Called after every action so the open screen refetches through the SDK. */
		onrefresh: () => void;
		/** Called after "Reset demo" instead of onrefresh (crew ids change on reseed). */
		onreset: () => void;
	};
	let { onrefresh, onreset }: Props = $props();

	const KEY = 'baseline.presenterOpen';
	const browser = typeof localStorage !== 'undefined'; // the app is client-only (ssr = false)
	let open = $state(browser && localStorage.getItem(KEY) === '1');
	let busy = $state(false);
	let confirmReset = $state(false);
	let error = $state('');

	const m = $derived(mission.state);
	// Fast-forward is 1 mission day per 5 s; real time is rate 1.
	const fastForward = $derived((m?.clockRate ?? 1) > 1);

	function toggle() {
		open = !open;
		if (browser) localStorage.setItem(KEY, open ? '1' : '0');
	}

	async function run(action: () => Promise<unknown>, after: () => void = onrefresh) {
		busy = true;
		error = '';
		try {
			await action();
			await mission.refresh();
			after();
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

	const JUMPS = [
		{ label: '+1 day', seconds: 86400 },
		{ label: '+1 week', seconds: 604800 },
		{ label: '+3 weeks', seconds: 1814400 }
	];
	const btn =
		'min-h-11 rounded-lg border border-muted/50 bg-bg px-3 text-sm font-medium text-ink transition hover:bg-raised active:scale-[0.97] disabled:opacity-50';
</script>

<!-- In the page flow under the footer, never fixed over the crew or ground UI. -->
<aside
	class="border-t-2 border-dashed border-muted/60 bg-[repeating-linear-gradient(135deg,#0a0f1c_0_12px,#0e1424_12px_24px)] px-6 py-2 print:hidden"
	aria-label="Presenter demo controls"
>
	<div class="flex flex-wrap items-center gap-3">
		<button type="button" class={btn} aria-expanded={open} aria-controls="presenter-controls" onclick={toggle}>
			{open ? '▾' : '▸'} PRESENTER
		</button>
		<span class="text-xs tracking-wider text-muted uppercase">demo controls, not part of the crew app</span>
		<span class="ml-auto font-mono text-sm text-muted tabular-nums">
			{#if m}MET {m.met} · ×{Math.round(m.clockRate)} {fastForward ? '· fast-forward' : '· real time'} · {m.linkUp ? 'link up' : 'link down'}{:else}no mission state{/if}
		</span>
	</div>

	{#if open}
		<div id="presenter-controls" class="flex flex-wrap items-center gap-2 pt-2 pb-1">
			<button
				type="button"
				class="{btn} {fastForward ? 'border-watch text-watch' : ''}"
				aria-pressed={fastForward}
				disabled={busy}
				onclick={() => run(() => api.demo.clock({ fastForward: !fastForward }))}
			>
				Fast-forward: {fastForward ? 'on' : 'off'}
			</button>
			{#each JUMPS as j (j.seconds)}
				<button type="button" class={btn} disabled={busy} onclick={() => run(() => api.demo.clock({ advanceSeconds: j.seconds }))}>
					{j.label}
				</button>
			{/each}
			<span class="mx-1 h-6 w-px bg-line" aria-hidden="true"></span>
			<button type="button" class={btn} disabled={busy} onclick={() => run(() => api.demo.scene5())}>Scene 5 (chest pain)</button>
			<button
				type="button"
				class="{btn} {m && !m.linkUp ? 'border-watch text-watch' : ''}"
				aria-pressed={m ? !m.linkUp : undefined}
				disabled={busy || !m}
				onclick={() => run(() => api.demo.link(!m!.linkUp))}
			>
				Link: {m?.linkUp === false ? 'down' : 'up'} (tap to {m?.linkUp === false ? 'restore' : 'cut'})
			</button>
			<span class="mx-1 h-6 w-px bg-line" aria-hidden="true"></span>
			{#if confirmReset}
				<span class="text-sm text-act-ink">Reload all demo data? Everything entered is lost.</span>
				<button type="button" class="{btn} border-act text-act-ink" disabled={busy} onclick={() => run(() => api.demo.reseed(), onreset)}>
					Yes, reset
				</button>
				<button type="button" class={btn} disabled={busy} onclick={() => (confirmReset = false)}>Cancel</button>
			{:else}
				<button type="button" class={btn} disabled={busy} onclick={() => (confirmReset = true)}>Reset demo…</button>
			{/if}
			{#if busy}<span class="text-sm text-muted" role="status">Working…</span>{/if}
			{#if error}<span class="text-sm text-act-ink" role="alert">{error}</span>{/if}
		</div>
	{/if}
</aside>
