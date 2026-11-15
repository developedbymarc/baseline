<script lang="ts">
	import { goto } from '$app/navigation';
	import { onMount } from 'svelte';
	import { api, errorMessage, type Level, type Metric, type Reminder } from '#lib/api/index.ts';
	import { fmt, formatMet, worst } from '#lib/format.ts';
	import { chestLevels, latestReadings, levelsOf, type ChestLevels } from '#lib/chest.ts';
	import { flow } from '#lib/stores/flow.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import Button from '#lib/components/Button.svelte';
	import ErrorBox from '#lib/components/ErrorBox.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import StatusBadge from '#lib/components/StatusBadge.svelte';
	import ChestFigure from '#lib/components/ChestFigure.svelte';

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
	let chest = $state<ChestLevels>({ heart: null, lungs: null });
	let loaded = $state(false);
	let error = $state('');
	let reporting = $state(false);
	let showAll = $state(false);

	// Most urgent first (overdue, then by due time). Three are shown so the main actions stay above
	// the fold on a landscape tablet; the rest are one tap away.
	const SHOWN = 3;
	const sorted = $derived(
		[...reminders].sort(
			(a, b) =>
				Number(b.overdue) - Number(a.overdue) ||
				Number(!!b.due) - Number(!!a.due) ||
				Number(a.dueMet ?? 0) - Number(b.dueMet ?? 0)
		)
	);
	const visible = $derived(showAll ? sorted : sorted.slice(0, SHOWN));

	const nameOf = (code: string) => metrics.find((m) => m.code === code)?.name ?? code;

	/** Reminders, tiles and chest figure; retried from the error box. */
	async function load() {
		error = '';
		try {
			[metrics, reminders] = await Promise.all([api.metrics(), api.reminders(session.crewId)]);
			// One fetch per metric feeds both the tiles and the chest figure.
			const latest = await latestReadings(
				session.crewId,
				TILES.flatMap((t) => t.codes)
			);
			const levels = levelsOf(latest);
			tiles = TILES.map(({ label, codes }) => {
				const readings = codes.map((c) => latest[c]);
				return {
					label,
					unit: metrics.find((m) => m.code === codes[0])?.unit ?? '',
					text: readings.every(Boolean) ? readings.map((r) => fmt(r!.value)).join('/') : '–',
					level: worst(codes.map((c) => levels[c]))
				};
			});
			chest = chestLevels(levels);
			loaded = true;
		} catch (e) {
			error = errorMessage(e);
		}
	}
	onMount(load);

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

<svelte:head><title>Home · BASELINE crew</title></svelte:head>

<h1 class="sr-only">Crew home</h1>
<div class="grid gap-8 lg:grid-cols-2">
	<section class="flex flex-col gap-4" aria-labelledby="due">
		<h2 id="due" class="font-display text-2xl font-semibold">Measurements</h2>

		{#each visible as r, i (r.id ?? r.metricCode)}
			<!-- Portrait (one column): two cards before "Show all", so the main actions stay on screen. -->
			<a
				href="/crew/measure?metric={r.metricCode}"
				class="flex min-h-18 items-center gap-4 rounded-2xl border p-4 transition hover:brightness-125 active:brightness-90 {i >=
					2 && !showAll
					? 'max-lg:hidden'
					: ''}
					{r.overdue
					? 'border-watch bg-watch-surface'
					: r.due
						? 'border-action bg-surface'
						: 'border-line bg-surface'}"
			>
				<!-- Overdue / due now / later: told apart by icon and text, not colour alone. -->
				<Icon
					name={r.overdue ? 'triangle' : 'clock'}
					class="size-7 shrink-0 {r.overdue ? 'text-watch' : r.due ? 'text-action' : 'text-muted'}"
				/>
				<span class="flex-1">
					<span class="block text-lg font-semibold {r.overdue || r.due ? '' : 'text-muted'}"
						>{nameOf(r.metricCode)}</span
					>
					<span
						class="block text-sm {r.overdue ? 'text-watch' : r.due ? 'text-ink' : 'text-muted'}"
					>
						{#if r.overdue}Overdue{r.dueMet != null ? ` · was due ${formatMet(r.dueMet)} MET` : ''}
						{:else if r.due}Due now
						{:else}Later{r.dueMet != null ? ` · due ${formatMet(r.dueMet)} MET` : ''}{/if}
					</span>
				</span>
				<span class="font-display text-lg font-semibold">Log</span>
			</a>
		{:else}
			<p class="rounded-2xl border border-line bg-surface p-4 text-muted">
				{loaded ? 'Nothing scheduled right now.' : 'Loading…'}
			</p>
		{/each}

		{#if sorted.length > 2}
			<!-- Landscape shows three, so with exactly three the button is for portrait only. -->
			<Button
				variant="secondary"
				class={sorted.length <= SHOWN ? 'lg:hidden' : ''}
				aria-expanded={showAll}
				onclick={() => (showAll = !showAll)}
			>
				{showAll ? 'Show fewer' : `Show all (${sorted.length})`}
			</Button>
		{/if}

		<p class="text-sm text-muted">
			Schedule set before launch. It keeps running with the link down.
		</p>

		<Button variant="danger" size="lg" class="mt-auto" disabled={reporting} onclick={reportSymptom}>
			<Icon name="octagon" class="size-6" /> Report a symptom
		</Button>
	</section>

	<section class="flex flex-col gap-4" aria-labelledby="latest">
		<h2 id="latest" class="font-display text-2xl font-semibold">Latest readings</h2>
		<div class="flex gap-4">
			<div class="shrink-0">
				{#if loaded}<ChestFigure
						heart={chest.heart}
						lungs={chest.lungs}
						heightClass="h-[150px] lg:h-[190px]"
					/>{/if}
			</div>
			<ul class="grid min-w-0 flex-1 grid-cols-2 content-start gap-3 lg:grid-cols-1">
				{#if !loaded && !error}<li
						class="rounded-2xl border border-line bg-surface px-4 py-6 text-muted"
					>
						Loading readings…
					</li>{/if}
				{#each tiles as t (t.label)}
					<li class="rounded-2xl border border-line bg-surface px-4 py-3">
						<p class="text-sm text-muted">{t.label}</p>
						<p class="flex flex-wrap items-baseline justify-between gap-x-3">
							<span class="font-display text-3xl font-semibold tabular-nums">
								{t.text}{#if t.text !== '–'}<span class="ml-1 text-lg font-normal text-muted"
										>{t.unit}</span
									>{/if}
							</span>
							{#if t.level}<StatusBadge level={t.level} />{/if}
						</p>
					</li>
				{/each}
			</ul>
		</div>

		<div class="mt-auto flex gap-4">
			<Button href="/crew/measure" size="lg" class="flex-1">New measurement</Button>
			<Button href="/crew/trends" variant="secondary" size="lg">Trends</Button>
		</div>
	</section>
</div>

{#if error}<div class="mt-6">
		<ErrorBox message={error} onretry={loaded ? undefined : load} />
	</div>{/if}
