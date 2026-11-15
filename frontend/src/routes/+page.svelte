<script lang="ts">
	import { goto } from '$app/navigation';
	import { api, errorMessage, type CrewMember } from '#lib/api/index.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import Button from '#lib/components/Button.svelte';

	let crew = $state<CrewMember[] | null>(null);
	let selectedId = $state('');
	let loading = $state(false);
	let error = $state('');

	const selected = $derived(crew?.find((m) => m.id === selectedId) ?? null);

	// Mock auth. The crew list is only readable by ground control, so we briefly
	// sign in as Ground to fetch it, then clear the session until a member is picked.
	async function chooseCrew() {
		error = '';
		loading = true;
		session.signIn('Ground');
		try {
			crew = await api.crew();
			selectedId = '';
		} catch (e) {
			error = errorMessage(e);
		} finally {
			session.signOut();
			loading = false;
		}
	}

	function chooseGround() {
		session.signIn('Ground');
		goto('/ground');
	}

	// The choice becomes X-Role / X-Crew-Id on every request.
	function signInAsCrew() {
		if (!selected) return;
		session.signIn('Crew', selected);
		goto('/crew');
	}
</script>

<main class="mx-auto grid min-h-dvh w-full max-w-xl content-center gap-8 px-6 py-12">
	<header class="space-y-2">
		<h1 class="font-display text-4xl font-bold tracking-widest">BASELINE</h1>
		<p class="text-muted">Decision support, not diagnosis. All crew data is synthetic.</p>
	</header>

	{#if crew}
		<section class="space-y-3" aria-labelledby="who">
			<h2 id="who" class="font-display text-2xl font-semibold">Who are you?</h2>
			<label class="block space-y-2">
				<span class="text-muted">Crew member</span>
				<select
					bind:value={selectedId}
					class="min-h-14 w-full rounded-2xl border border-line bg-surface px-4 text-lg text-ink"
				>
					<option value="" disabled>Select a crew member…</option>
					{#each crew as member (member.id)}
						<option value={member.id}>{member.name} · {member.role}</option>
					{/each}
				</select>
			</label>
			<Button size="lg" class="w-full" disabled={!selected} onclick={signInAsCrew}>
				{selected ? `Sign in as ${selected.name}` : 'Sign in'}
			</Button>
			<Button variant="secondary" class="w-full" onclick={() => (crew = null)}>Back</Button>
		</section>
	{:else}
		<section class="space-y-3" aria-labelledby="role">
			<h2 id="role" class="font-display text-2xl font-semibold">Sign in as</h2>
			<Button size="lg" class="w-full" disabled={loading} onclick={chooseCrew}>
				{loading ? 'Loading crew…' : 'Crew member'}
			</Button>
			<Button variant="secondary" size="lg" class="w-full" onclick={chooseGround}
				>Ground control</Button
			>
		</section>
	{/if}

	{#if error}<p role="alert" class="rounded-xl border border-act bg-act-surface p-4 text-act-ink">
			{error}
		</p>{/if}
</main>
