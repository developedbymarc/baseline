<script lang="ts">
	import { onMount } from 'svelte';
	import { api, errorMessage, type LinkMessage, type Packet } from '#lib/api/index.ts';
	import { latencyText } from '#lib/format.ts';
	import { flow } from '#lib/stores/flow.svelte.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import Button from '#lib/components/Button.svelte';
	import PacketBoxes from '#lib/components/PacketBoxes.svelte';

	const MAX_NOTE = 500;

	let packet = $state<Packet | null>(flow.packet);
	let note = $state('');
	let sent = $state<LinkMessage | null>(null);
	let error = $state('');
	let sending = $state(false);

	// The draft is rebuilt from the latest data. Fall back to the one that came with the result.
	onMount(async () => {
		try {
			packet = await api.packetDraft(session.crewId, flow.alert?.alertId, flow.run?.id);
		} catch (e) {
			if (!packet) error = errorMessage(e);
		}
	});

	async function send() {
		sending = true;
		error = '';
		try {
			sent = await api.sendPacket(
				session.crewId,
				flow.alert?.alertId ?? packet?.alertId ?? null,
				flow.run?.id ?? packet?.runId ?? null,
				note.trim()
			);
			flow.closeAlert();
		} catch (e) {
			error = errorMessage(e);
		} finally {
			sending = false;
		}
	}

	const arrival = $derived(
		sent?.state === 'held'
			? 'Held until the link returns.'
			: `Sent. Arrives in about ${Math.max(1, Math.round((sent?.secondsUntilDelivery ?? 0) / 60))} min.`
	);
</script>

<div class="space-y-6">
	<header class="flex items-baseline justify-between gap-4">
		<h1 class="font-display text-3xl font-semibold">Handoff packet for ground</h1>
		{#if packet}
			<p class="text-sm text-muted">Built by the app from your data · written {packet.createdMet} MET</p>
		{/if}
	</header>

	{#if packet}
		<PacketBoxes {packet} />

		{#if sent}
			<div role="status" class="flex items-center justify-between gap-4 rounded-2xl border border-ok bg-ok-surface p-5">
				<p class="text-lg font-semibold text-ok">{arrival}</p>
				<Button href="/crew">Back to home</Button>
			</div>
		{:else}
			<div class="grid items-end gap-4 md:grid-cols-[1fr_16rem]">
				<div>
					<label for="note" class="mb-2 block text-sm text-muted">Add a short note (optional)</label>
					<textarea
						id="note"
						rows="2"
						maxlength={MAX_NOTE}
						bind:value={note}
						class="w-full resize-none rounded-2xl border border-line bg-surface p-4 text-lg focus:border-action focus:outline-none"
					></textarea>
					<p class="mt-1 text-right text-xs text-muted">{note.length} / {MAX_NOTE}</p>
				</div>
				<div class="space-y-2 text-center">
					<Button size="lg" class="w-full" disabled={sending} onclick={send}>
						{sending ? 'Sending…' : 'Send to ground'}
					</Button>
					<p class="text-xs text-muted">
						{mission.state ? `Arrives about ${latencyText(mission.state.oneWayLatencySeconds)} after sending` : ''}
					</p>
				</div>
			</div>
		{/if}
	{:else if !error}
		<p class="text-muted">Building packet…</p>
	{/if}

	{#if error}<p role="alert" class="rounded-xl border border-act bg-act-surface p-4 text-act-ink">{error}</p>{/if}
</div>
