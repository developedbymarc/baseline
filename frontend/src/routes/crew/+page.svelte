<script lang="ts">
	import { goto } from '$app/navigation';
	import { onMount } from 'svelte';
	import { api, errorMessage, type Level, type Metric, type Reminder } from '#lib/api/index.ts';
	import { fmt, formatMet, shownLevel, worst } from '#lib/format.ts';
	import { flow } from '#lib/stores/flow.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import Button from '#lib/components/Button.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import StatusBadge from '#lib/components/StatusBadge.svelte';

	interface Tile {
		label: string;
		text: string;
		unit: string;
		level: Level | null;
	}

	// Latest readings: the four tiles from ui-spec §3.1. BP combines bp_sys and bp_dia.
	const TILES = [
		{ label: 'Resting HR', codes: ['hr_rest'] },
		{ label: 'Blood pressure', codes: ['bp_sys', 'bp_dia'] },
		{ label: 'Temperature', codes: ['temp'] },
		{ label: 'SpO2', codes: ['spo2'] }
	];

	let metrics = $state<Metric[]>([]);
	let reminders = $state<Reminder[]>([]);
	let tiles = $state<Tile[]>([]);
	let error = $state('');
	let reporting = $state(false);
	let showAll = $state(false);

	// Most urgent first (overdue, then by due time). Three are shown so the main actions stay above
	// the fold on a landscape tablet; the rest are one tap away.
	const SHOWN = 3;
	const sorted = $derived(
		[...reminders].sort((a, b) => Number(b.overdue) - Number(a.overdue) || Number(a.dueMet ?? 0) - Number(b.dueMet ?? 0))
	);
	const visible = $derived(showAll ? sorted : sorted.slice(0, SHOWN));

	const nameOf = (code: string) => metrics.find((m) => m.code === code)?.name ?? code;

	async function latest(code: string) {
		const points = await api.measurements(session.crewId, code, 7);
		return [...points].sort((a, b) => Date.parse(a.recordedAt) - Date.parse(b.recordedAt)).at(-1);
	}

	onMount(async () => {
		try {
			[metrics, reminders] = await Promise.all([api.metrics(), api.reminders(session.crewId)]);
			tiles = await Promise.all(
				TILES.map(async ({ label, codes }) => {
					const readings = await Promise.all(codes.map(latest));
					const unit = metrics.find((m) => m.code === codes[0])?.unit ?? '';
					return {
						label,
						unit,
						text: readings.every(Boolean) ? readings.map((r) => fmt(r!.value)).join('/') : '–',
						level: worst(readings.map((r) => (r ? shownLevel(r) : null)))
					};
				})
			);
		} catch (e) {
			error = errorMessage(e);
		}
	});

	async function reportSymptom() {
		reporting = true;
		error = '';
		try {
			const res = await api.logSymptom(session.crewId, 'chest_pain');
			if (res.protocolId) {
				flow.openAlert({
					alertId: res.alertId,
					reason: 'Symptom reported: chest pain.',
					protocolId: res.protocolId
				});
				await goto('/crew/protocol');
			}
		} catch (e) {
			error = errorMessage(e);
		} finally {
			reporting = false;
		}
	}
</script>

<div class="grid gap-8 lg:grid-cols-2">
	<section class="flex flex-col gap-4" aria-labelledby="due">
		<h2 id="due" class="font-display text-2xl font-semibold">Due now</h2>

		{#each visible as r (r.id ?? r.metricCode)}
			<a
				href="/crew/measure?metric={r.metricCode}"
				class="flex min-h-18 items-center gap-4 rounded-2xl border p-4 transition hover:brightness-125
					{r.overdue ? 'border-watch bg-watch-surface' : 'border-line bg-surface'}"
			>
				<Icon name="clock" class="size-7 shrink-0 {r.overdue ? 'text-watch' : 'text-action'}" />
				<span class="flex-1">
					<span class="block text-lg font-semibold">{nameOf(r.metricCode)}</span>
					<span class="block text-sm {r.overdue ? 'text-watch' : 'text-muted'}">
						{#if r.overdue}Overdue{r.dueMet ? ` · was due ${formatMet(r.dueMet)} MET` : ''}{:else}Due{r.dueMet
								? ` ${formatMet(r.dueMet)} MET`
								: ''}{/if}
					</span>
				</span>
				<span class="font-display text-lg font-semibold">Log</span>
			</a>
		{:else}
			<p class="rounded-2xl border border-line bg-surface p-4 text-muted">Nothing due right now.</p>
		{/each}

		{#if sorted.length > SHOWN}
			<Button variant="secondary" aria-expanded={showAll} onclick={() => (showAll = !showAll)}>
				{showAll ? 'Show fewer' : `Show all (${sorted.length})`}
			</Button>
		{/if}

		<p class="text-sm text-muted">Schedule set before launch. It keeps running with the link down.</p>

		<Button variant="danger" size="lg" class="mt-auto" disabled={reporting} onclick={reportSymptom}>
			<Icon name="octagon" class="size-6" /> Report a symptom
		</Button>
	</section>

	<section class="flex flex-col gap-4" aria-labelledby="latest">
		<h2 id="latest" class="font-display text-2xl font-semibold">Latest readings</h2>
		<div class="grid grid-cols-2 gap-4">
			{#each tiles as t (t.label)}
				<div class="rounded-2xl border border-line bg-surface p-4">
					<p class="text-sm text-muted">{t.label}</p>
					<p class="mt-1 font-display text-3xl font-semibold">
						{t.text}{#if t.text !== '–'}<span class="ml-1 text-lg font-normal text-muted">{t.unit}</span>{/if}
					</p>
					<div class="mt-2 min-h-5">{#if t.level}<StatusBadge level={t.level} />{/if}</div>
				</div>
			{/each}
		</div>

		<div class="mt-auto flex gap-4">
			<Button href="/crew/measure" size="lg" class="flex-1">New measurement</Button>
			<Button href="/crew/trends" variant="secondary" size="lg">Trends</Button>
		</div>
	</section>
</div>

{#if error}<p role="alert" class="mt-6 rounded-xl border border-act bg-act-surface p-4 text-act-ink">{error}</p>{/if}
