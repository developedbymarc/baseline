<script lang="ts">
	import { goto } from '$app/navigation';
	import { api } from '#lib/api/index.ts';
	import { toLevel } from '#lib/format.ts';
	import { flow } from '#lib/stores/flow.svelte.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import AlertBanner from '#lib/components/AlertBanner.svelte';
	import CrewBar from '#lib/components/CrewBar.svelte';
	import PresenterBar from '#lib/components/PresenterBar.svelte';
	import { crewList } from '#lib/demo.ts';

	let { children } = $props();
	/** Bumped by the presenter bar: re-mounts the open screen so it refetches. */
	let version = $state(0);

	// Reseed gives every crew member a new id: sign in again as the same person (by name), or go
	// back to the role picker if they cannot be found.
	async function afterReset() {
		const name = session.crewName;
		const member = (await crewList().catch(() => [])).find((c) => c.name === name);
		// New id first, then clear the flow, so nothing refetches with the old (deleted) id.
		if (member) session.signIn('Crew', member);
		else session.signOut();
		flow.resetAll();
		if (member) {
			version++;
			await goto('/crew');
		} else await goto('/');
	}

	// After a presenter action: refetch the open screen and pick up a new open red alert
	// (e.g. the chest pain example when signed in as that crew member).
	function afterAction() {
		version++;
		checkAlerts();
	}
	const signedIn = $derived(session.role === 'Crew' && !!session.crewId);

	$effect(() => {
		if (!signedIn) goto('/');
	});

	$effect(() => (signedIn ? mission.poll() : undefined));

	// Keep the banner up after a reload: an unacknowledged red alert is still open.
	function checkAlerts() {
		if (!signedIn || flow.alert) return;
		api
			.alerts(session.crewId)
			.then((alerts) => {
				const open = alerts.find((a) => toLevel(a.level) === 'Red' && !a.acknowledgedBy);
				if (open) flow.openAlert({ alertId: open.id, reason: open.reason, protocolId: null });
			})
			.catch(() => {});
	}
	$effect(checkAlerts);
</script>

{#if signedIn}
	<div class="flex min-h-dvh flex-col">
		<AlertBanner />
		<CrewBar />
		<main class="mx-auto w-full max-w-6xl flex-1 px-6 py-6">{#key version}{@render children()}{/key}</main>
		<footer class="border-t border-line px-6 py-3 text-xs text-muted">
			Decision support, not diagnosis. All crew data is synthetic.
		</footer>
		<PresenterBar onrefresh={afterAction} onreset={afterReset} />
	</div>
{/if}
