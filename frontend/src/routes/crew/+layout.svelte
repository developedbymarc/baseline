<script lang="ts">
	import { goto } from '$app/navigation';
	import { api } from '#lib/api/index.ts';
	import { toLevel } from '#lib/format.ts';
	import { flow } from '#lib/stores/flow.svelte.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import AlertBanner from '#lib/components/AlertBanner.svelte';
	import CrewBar from '#lib/components/CrewBar.svelte';

	let { children } = $props();
	const signedIn = $derived(session.role === 'Crew' && !!session.crewId);

	$effect(() => {
		if (!signedIn) goto('/');
	});

	$effect(() => (signedIn ? mission.poll() : undefined));

	// Keep the banner up after a reload: an unacknowledged red alert is still open.
	$effect(() => {
		if (!signedIn || flow.alert) return;
		api
			.alerts(session.crewId)
			.then((alerts) => {
				const open = alerts.find((a) => toLevel(a.level) === 'Red' && !a.acknowledgedBy);
				if (open) flow.openAlert({ alertId: open.id, reason: open.reason, protocolId: null });
			})
			.catch(() => {});
	});
</script>

{#if signedIn}
	<div class="flex min-h-dvh flex-col">
		<AlertBanner />
		<CrewBar />
		<main class="mx-auto w-full max-w-6xl flex-1 px-6 py-6">{@render children()}</main>
		<footer class="border-t border-line px-6 py-3 text-xs text-muted">
			Decision support, not diagnosis. All crew data is synthetic.
		</footer>
	</div>
{/if}
