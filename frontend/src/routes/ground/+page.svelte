<script lang="ts">
	import { onMount } from 'svelte';
	import {
		api, errorMessage, type LinkMessage, type Metric, type OverviewCell, type OverviewRow
	} from '#lib/api/index.ts';
	import { LEVEL_WORD, ageText, fmt, shownLevel, toLevel } from '#lib/format.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import StatusBadge from '#lib/components/StatusBadge.svelte';

	let metrics = $state<Metric[]>([]);
	let rows = $state<OverviewRow[]>([]);
	let inbox = $state<LinkMessage[]>([]);
	let error = $state('');

	const columns = $derived(metrics.some((m) => m.hasStatus) ? metrics.filter((m) => m.hasStatus) : metrics);
	const lowered = (c: OverviewCell) => !!c.context?.downgraded;
	const cellFor = (row: OverviewRow, code: string) =>
		row.cells?.find((c) => c.metricCode === code && c.value != null && c.recordedAt) ?? null;

	async function refresh() {
		try {
			[rows, inbox] = await Promise.all([api.overview(), api.inbox()]);
			error = '';
		} catch (e) {
			error = errorMessage(e);
		}
	}

	onMount(() => {
		api.metrics().then((m) => (metrics = m)).catch((e) => (error = errorMessage(e)));
		refresh();
		const timer = setInterval(refresh, 5000);
		return () => clearInterval(timer);
	});

	const kind = (m: LinkMessage) => (m.payload?.data ? 'Packet' : 'Alert');
</script>

<div class="grid gap-8 lg:grid-cols-[1fr_20rem]">
	<section class="space-y-4" aria-labelledby="crew">
		<h1 id="crew" class="sr-only">Crew overview</h1>
		<div class="overflow-x-auto rounded-2xl border border-line bg-surface">
			<table class="w-full text-left">
				<thead class="text-sm text-muted">
					<tr class="border-b border-line">
						<th scope="col" class="px-4 py-3 font-medium">Crew</th>
						{#each columns as m (m.code)}<th scope="col" class="px-4 py-3 font-medium">{m.name}</th>{/each}
					</tr>
				</thead>
				<tbody>
					{#each rows as row (row.crewMemberId)}
						<tr class="border-b border-line last:border-0">
							<th scope="row" class="px-4 py-3 font-semibold">
								{row.name}<span class="block text-sm font-normal text-muted">{row.role}</span>
							</th>
							{#each columns as m (m.code)}
								{@const cell = cellFor(row, m.code)}
								{@const level = cell ? shownLevel(cell) : null}
								<td class="p-0">
									{#if cell && level}
										<a
											href="/ground/reports?crew={row.crewMemberId}"
											class="block px-4 py-3 hover:bg-raised {level === 'Red' ? 'bg-act-surface' : ''}"
										>
											<StatusBadge {level} asterisk={lowered(cell)} />
											<span class="ml-1 font-display text-lg font-semibold">{fmt(cell.value)}</span>
											<span class="block text-xs text-muted">
												{mission.state && cell.recordedAt ? ageText(cell.recordedAt, mission.state.missionNow) : ''}
												{#if lowered(cell)}· lowered from {LEVEL_WORD[toLevel(cell.context?.originalLevel) ?? 'Yellow']}{/if}
											</span>
										</a>
									{:else}
										<span class="block px-4 py-3 text-muted">nothing yet</span>
									{/if}
								</td>
							{/each}
						</tr>
					{:else}
						<tr><td class="px-4 py-6 text-muted" colspan={columns.length + 1}>No crew data has arrived yet.</td></tr>
					{/each}
				</tbody>
			</table>
		</div>

		<p class="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted">
			<StatusBadge level="Green" /><StatusBadge level="Yellow" /><StatusBadge level="Red" />
			<span>Shape and word always shown with the colour. * lowered by context.</span>
		</p>
	</section>

	<aside class="space-y-3" aria-labelledby="arrived">
		<h2 id="arrived" class="font-display text-2xl font-semibold">Arrived</h2>
		{#each inbox.slice(0, 8) as m (m.id)}
			<a
				href="/ground/inbox?id={m.id}"
				class="block rounded-2xl border p-4 hover:bg-raised {kind(m) === 'Packet' ? 'border-act bg-act-surface' : 'border-line bg-surface'}"
			>
				<p class="font-semibold">{kind(m)}{m.payload?.crewName ? ` · ${m.payload.crewName}` : ''}</p>
				<p class="mt-1 text-sm">{m.payload?.data?.trigger.summary ?? m.payload?.reason ?? ''}</p>
				<p class="mt-1 text-xs text-muted">{m.ageOfData}</p>
			</a>
		{:else}
			<p class="text-sm text-muted">Nothing has arrived. Ground cannot see messages still in flight.</p>
		{/each}
	</aside>
</div>

{#if error}<p role="alert" class="mt-6 rounded-xl border border-act bg-act-surface p-4 text-act-ink">{error}</p>{/if}
