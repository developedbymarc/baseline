<script lang="ts">
	import { goto } from '$app/navigation';
	import { page } from '$app/state';
	import { latencyText } from '#lib/format.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import PresenterBar from '#lib/components/PresenterBar.svelte';
	import OfflineBanner from '#lib/components/OfflineBanner.svelte';

	let { children } = $props();
	/** Bumped by the presenter bar: re-mounts the open screen so it refetches. */
	let version = $state(0);
	const signedIn = $derived(session.role === 'Ground');

	const NAV = [
		{ href: '/ground', label: 'Crew' },
		{ href: '/ground/inbox', label: 'Inbox' },
		{ href: '/ground/reports', label: 'Reports' }
	];

	$effect(() => {
		if (!signedIn) goto('/');
	});
	$effect(() => (signedIn ? mission.poll() : undefined));
</script>

{#if signedIn}
	<div class="flex min-h-dvh flex-col">
		<OfflineBanner />
		<header
			class="grid grid-cols-[1fr_auto_1fr] items-center gap-4 border-b border-line bg-surface px-6 py-3 print:hidden"
		>
			<div class="flex items-center gap-3">
				<span class="font-display text-xl font-bold tracking-widest">BASELINE · GROUND</span>
				<span class="rounded border border-line px-2 py-0.5 text-xs text-muted">SYNTHETIC DATA</span
				>
			</div>

			<nav class="flex gap-2" aria-label="Ground">
				{#each NAV as item (item.href)}
					{@const active =
						item.href === '/ground'
							? page.url.pathname === item.href
							: page.url.pathname.startsWith(item.href)}
					<a
						href={item.href}
						aria-current={active ? 'page' : undefined}
						class="inline-flex min-h-11 items-center rounded-lg px-4 text-sm font-medium {active
							? 'bg-raised text-ink'
							: 'text-muted hover:text-ink'}"
					>
						{item.label}
					</a>
				{/each}
			</nav>

			<div class="flex items-center justify-end gap-4 text-sm text-muted">
				<span
					>{mission.offline
						? 'Backend unreachable'
						: mission.state
							? `Mission now ${mission.state.met}`
							: 'Connecting…'}</span
				>
				<button
					type="button"
					class="min-h-11 rounded-lg px-3 font-semibold text-ink hover:bg-raised"
					onclick={() => {
						session.signOut();
						goto('/');
					}}
				>
					Sign out
				</button>
			</div>
		</header>

		<aside
			class="border-b border-line bg-raised/60 px-6 py-2 text-sm print:hidden"
			aria-label="About this data"
		>
			Everything here is what has arrived through the link.
			{#if mission.state}Crew data is at least {latencyText(mission.state.oneWayLatencySeconds)} old.{/if}
		</aside>

		<main class="mx-auto w-full max-w-360 flex-1 px-6 py-6">
			{#key version}{@render children()}{/key}
		</main>
		<PresenterBar
			onrefresh={() => version++}
			onreset={() => {
				version++;
			}}
		/>
	</div>
{/if}
