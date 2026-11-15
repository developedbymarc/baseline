<script lang="ts">
	import { page } from '$app/state';
	import { onMount } from 'svelte';
	import { api, errorMessage, type Baseline, type Metric, type Reading } from '#lib/api/index.ts';
	import { LEVEL_WORD, fmt, metShort, shownLevel } from '#lib/format.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import Button from '#lib/components/Button.svelte';
	import ErrorBox from '#lib/components/ErrorBox.svelte';
	import StatusBadge from '#lib/components/StatusBadge.svelte';
	import TrendChart, { type Band, type TrendPoint } from '#lib/components/TrendChart.svelte';

	const RANGES = [
		{ label: '7 days', days: 7 },
		{ label: '14 days', days: 14 },
		{ label: '30 days', days: 30 },
		{ label: 'Full', days: 0 }
	];

	let metrics = $state<Metric[]>([]);
	let baselines = $state<Baseline[]>([]);
	let readings = $state<Reading[]>([]);
	let code = $state('');
	let days = $state(7);
	let error = $state('');
	let loading = $state(true);
	/** Bumped by "Try again" to rerun the readings request. */
	let retry = $state(0);

	const metric = $derived(metrics.find((m) => m.code === code));
	const sorted = $derived([...readings].sort((a, b) => Date.parse(a.recordedAt) - Date.parse(b.recordedAt)));
	const latest = $derived(sorted.at(-1));
	const latestLevel = $derived(latest ? shownLevel(latest) : null);

	const points = $derived<TrendPoint[]>(
		// Mission time on the axis (and in the chart's aria description), never wall-clock dates.
		sorted.map((r) => ({
			label: mission.state ? `MET ${metShort(r.recordedAt, mission.state.launchDate)}` : (r.recordedMet ?? r.recordedAt),
			value: r.value,
			level: shownLevel(r)
		}))
	);

	const normal = $derived<Band | null>(
		metric?.normalLow != null && metric.normalHigh != null
			? { low: metric.normalLow, high: metric.normalHigh, label: 'normal band, chart only' }
			: null
	);

	// Personal baseline band: mean ± zWarn × sdEffective.
	const baseline = $derived.by<Band | null>(() => {
		const b = baselines.find((x) => x.metricCode === code);
		const z = metric?.zWarn;
		if (!b || z == null) return null;
		return {
			low: b.mean - z * b.sdEffective,
			high: b.mean + z * b.sdEffective,
			label: `your baseline ±${fmt(z)} SD`
		};
	});

	const summary = $derived.by(() => {
		if (!latest || !metric) return 'No readings in this range.';
		const values = sorted.map((r) => r.value);
		const range = `${sorted.length} readings, from ${fmt(Math.min(...values))} to ${fmt(Math.max(...values))} ${metric.unit}`;
		const status = latestLevel ? `, status ${LEVEL_WORD[latestLevel]}` : '';
		const b = baselines.find((x) => x.metricCode === code);
		const base = b ? ` Your baseline is ${fmt(b.mean)} ${metric.unit} (n=${b.n}).` : '';
		return `Latest ${fmt(latest.value)} ${metric.unit}${status}. ${range}.${base}`;
	});

	onMount(async () => {
		try {
			[metrics, baselines] = await Promise.all([api.metrics(), api.baselines(session.crewId)]);
			const wanted = page.url.searchParams.get('metric');
			code = metrics.find((m) => m.code === wanted)?.code ?? metrics[0]?.code ?? '';
		} catch (e) {
			error = errorMessage(e);
		}
	});

	$effect(() => {
		void retry;
		if (!code) return;
		let stale = false;
		loading = true;
		api
			.measurements(session.crewId, code, days || undefined)
			.then((r) => !stale && ((readings = r), (error = '')))
			.catch((e) => !stale && (error = errorMessage(e)))
			.finally(() => !stale && (loading = false));
		return () => (stale = true);
	});
</script>

<div class="space-y-6">
	<div class="flex flex-wrap gap-2" role="group" aria-label="Metric">
		{#each metrics as m (m.code)}
			<button
				type="button"
				aria-pressed={m.code === code}
				onclick={() => (code = m.code)}
				class="min-h-14 rounded-full border px-5 text-lg {m.code === code ? 'border-action bg-raised font-semibold' : 'border-line bg-surface text-muted hover:text-ink'}"
			>
				{m.name}
			</button>
		{/each}
	</div>

	{#if metric}
		<!-- Value, status and range on one row, so Done stays above the fold on a tablet. -->
		<header class="flex flex-wrap items-end justify-between gap-4">
			<div>
				<p class="text-base text-muted">{metric.name} · {RANGES.find((r) => r.days === days)?.label}</p>
				<div class="flex items-center gap-4">
					<p class="font-display text-6xl font-bold">
						{latest ? fmt(latest.value) : '–'} <span class="text-3xl font-medium text-muted">{metric.unit}</span>
					</p>
					{#if latestLevel}<StatusBadge level={latestLevel} />{/if}
				</div>
			</div>

			<div class="flex gap-2" role="group" aria-label="Range">
				{#each RANGES as r (r.label)}
					<button
						type="button"
						aria-pressed={r.days === days}
						onclick={() => (days = r.days)}
						class="min-h-14 rounded-xl border px-5 {r.days === days ? 'border-action bg-raised font-semibold' : 'border-line bg-surface text-muted'}"
					>
						{r.label}
					</button>
				{/each}
			</div>
		</header>

		<div class="rounded-2xl border border-line bg-surface p-4">
			{#if points.length}
				<TrendChart {points} unit={metric.unit} {normal} {baseline} />
			{:else}
				<p class="grid h-[clamp(200px,calc(100dvh-600px),384px)] place-items-center text-muted">{loading ? 'Loading readings…' : 'No readings in this range.'}</p>
			{/if}
		</div>

		<div class="flex items-center justify-between gap-4 rounded-2xl border border-line bg-surface p-4">
			<p>{summary}</p>
			<Button href="/crew" class="shrink-0">Done</Button>
		</div>
	{/if}

	{#if error}<ErrorBox message={error} onretry={() => retry++} />{/if}
</div>
