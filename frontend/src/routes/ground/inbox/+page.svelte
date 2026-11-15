<script lang="ts">
	import { page } from '$app/state';
	import { onMount } from 'svelte';
	import { api, errorMessage, type LinkMessage } from '#lib/api/index.ts';
	import { fmt, latencyText, metFromDates, toLevel } from '#lib/format.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import Button from '#lib/components/Button.svelte';
	import PacketBoxes from '#lib/components/PacketBoxes.svelte';
	import StatusBadge from '#lib/components/StatusBadge.svelte';
	import type { Packet } from '#lib/api/types.ts';

	let messages = $state<LinkMessage[]>([]);
	let selectedId = $state<string | null>(page.url.searchParams.get('id'));
	let showText = $state(false);
	let error = $state('');

	const selected = $derived(messages.find((m) => m.id === selectedId) ?? messages[0]);
	const packet = $derived(selected?.payload?.data ? (selected.payload as Packet) : null);
	const launch = $derived(mission.state?.launchDate);

	const delaySeconds = $derived(
		selected?.deliveredAt ? (Date.parse(selected.deliveredAt) - Date.parse(selected.sentAt)) / 1000 : null
	);

	onMount(() => {
		const load = () =>
			api.inbox().then((m) => ((messages = m), (error = ''))).catch((e) => (error = errorMessage(e)));
		load();
		const timer = setInterval(load, 5000);
		return () => clearInterval(timer);
	});

</script>

<div class="grid gap-6 lg:grid-cols-[16rem_1fr]">
	<nav class="space-y-2" aria-label="Messages">
		{#each messages as m (m.id)}
			<button
				type="button"
				aria-current={m.id === selected?.id}
				onclick={() => ((selectedId = m.id), (showText = false))}
				class="w-full rounded-xl border p-3 text-left {m.id === selected?.id ? 'border-action bg-raised' : 'border-line bg-surface'}"
			>
				<span class="block font-semibold">{m.payload?.data ? 'Packet' : 'Alert'}{m.payload?.crewName ? ` · ${m.payload.crewName}` : ''}</span>
				<span class="block text-xs text-muted">{m.ageOfData}</span>
			</button>
		{:else}
			<p class="text-sm text-muted">Nothing has arrived.</p>
		{/each}
	</nav>

	{#if selected}
		<div class="space-y-4">
			<header class="flex flex-wrap items-center gap-x-8 gap-y-2 rounded-2xl border border-act bg-act-surface px-5 py-4">
				<p class="font-display text-3xl font-bold">{selected.ageOfData}</p>
				<p class="text-sm">
					{#if launch}
						sent {packet?.createdMet ?? metFromDates(selected.sentAt, launch)} MET
						· arrived {selected.deliveredAt ? metFromDates(selected.deliveredAt, launch) : '–'} MET
					{/if}
					{#if delaySeconds != null}· link delay {latencyText(delaySeconds)}{/if}
				</p>
			</header>

			{#if packet}
				<div class="grid gap-4 xl:grid-cols-[1fr_20rem]">
					<div class="space-y-4">
						<PacketBoxes {packet} />

						<section class="rounded-2xl border border-line bg-surface p-5" aria-labelledby="readings">
							<h2 id="readings" class="mb-3 text-sm font-semibold text-muted">Readings, last 72 h</h2>
							<table class="w-full text-left text-sm">
								<tbody>
									{#each packet.data.measurements72h as r (r.recordedMet + r.metricName)}
										{@const level = toLevel(r.level)}
										<tr class="border-t border-line first:border-0">
											<td class="py-2 text-muted">{r.recordedMet}</td>
											<td class="py-2">{r.metricName}</td>
											<td class="py-2 text-right">{fmt(r.value)} {r.unit}</td>
											<td class="py-2 text-right">
												{#if r.recordedMet.startsWith('-')}<span class="text-muted">pre-flight</span>
												{:else if level}<StatusBadge {level} />{/if}
											</td>
										</tr>
									{/each}
								</tbody>
							</table>
						</section>
					</div>

					<aside class="space-y-4 self-start rounded-2xl border border-line bg-surface p-5" aria-labelledby="followups">
						<h2 id="followups" class="font-display text-xl font-semibold">Follow-ups, prepared from data</h2>
						{#each packet.data.followUp as f (f.key)}
							<div>
								<p class="font-semibold">{f.question}</p>
								<p>{f.answer}</p>
								<p class="text-xs text-muted">from {f.filledFrom}</p>
							</div>
						{/each}
						<Button variant="secondary" class="w-full" onclick={() => (showText = !showText)}>
							{showText ? 'Hide plain text' : 'Show plain text'}
						</Button>
					</aside>
				</div>

				{#if showText}
					<pre class="overflow-x-auto rounded-2xl border border-line bg-surface p-5 text-sm whitespace-pre-wrap">{packet.text}</pre>
				{/if}
			{:else}
				<p class="rounded-2xl border border-line bg-surface p-5 text-lg">{selected.payload?.reason ?? 'Alert received.'}</p>
			{/if}
		</div>
	{/if}
</div>

{#if error}<p role="alert" class="mt-6 rounded-xl border border-act bg-act-surface p-4 text-act-ink">{error}</p>{/if}
