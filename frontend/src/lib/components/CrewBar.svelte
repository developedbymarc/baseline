<script lang="ts">
	import { goto } from '$app/navigation';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import { latencyText, missionDay } from '#lib/format.ts';

	const m = $derived(mission.state);
	const link = $derived(
		mission.offline
			? 'Backend unreachable'
			: m
				? m.linkUp
					? `Link up · ${latencyText(m.oneWayLatencySeconds)} one-way`
					: 'Link down'
				: 'Connecting…'
	);
</script>

<!-- Three groups that never break inside; on a narrow screen the right group moves to a second row. -->
<header class="flex flex-wrap items-center justify-between gap-x-6 gap-y-2 border-b border-line bg-surface px-6 py-3">
	<div class="flex items-center gap-3 whitespace-nowrap">
		<a href="/crew" class="font-display text-xl font-bold tracking-widest">BASELINE</a>
		<span class="rounded border border-line px-2 py-0.5 text-xs text-muted">SYNTHETIC DATA</span>
	</div>

	<p class="font-display text-xl font-medium whitespace-nowrap tabular-nums">
		{#if m}MET {m.met}{#if missionDay(m.met)}<span class="text-muted">{' · '}Day {missionDay(m.met)}</span>{/if}{:else}MET –{/if}
	</p>

	<div class="flex items-center justify-end gap-3 text-sm whitespace-nowrap text-muted">
		<span class={mission.offline || m?.linkUp === false ? 'text-watch' : ''}>{link}</span>
		{#if m?.onboardFirst}<span class="rounded border border-line px-2 py-0.5 text-xs">on-board first</span>{/if}
		<button
			type="button"
			class="max-w-48 truncate font-semibold text-ink underline-offset-4 hover:underline"
			onclick={() => {
				session.signOut();
				goto('/');
			}}
		>
			{session.crewName}
		</button>
	</div>
</header>
