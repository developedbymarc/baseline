<script lang="ts">
	import { page } from '$app/state';
	import { onMount } from 'svelte';
	import {
		api, errorMessage, type CrewMember, type Report, type ReportPeriod
	} from '#lib/api/index.ts';
	import { fmt, signed } from '#lib/format.ts';
	import Button from '#lib/components/Button.svelte';
	import ErrorBox from '#lib/components/ErrorBox.svelte';

	const WINDOWS = [
		{ id: 'biweekly', label: 'Biweekly' },
		{ id: 'monthly', label: 'Monthly' }
	];

	let windowId = $state('biweekly');
	let periods = $state<ReportPeriod[]>([]);
	let period = $state<number | null>(null);
	let crew = $state<CrewMember[]>([]);
	let scope = $state(page.url.searchParams.get('crew') ?? 'crew');
	let report = $state<Report | null>(null);
	let error = $state('');
	/** Bumped by "Try again" to rerun the report request. */
	let retry = $state(0);

	const field = 'rounded-xl border border-line bg-surface px-3 py-2 text-sm';

	onMount(() => {
		api.crew().then((c) => (crew = c)).catch((e) => (error = errorMessage(e)));
	});

	$effect(() => {
		const w = windowId;
		api
			.reportPeriods(w)
			.then((p) => {
				periods = p;
				period = (p.find((x) => !x.complete) ?? p.at(-1))?.number ?? null;
			})
			.catch((e) => (error = errorMessage(e)));
	});

	$effect(() => {
		void retry;
		if (period == null) return;
		let stale = false;
		(scope === 'crew' ? api.crewReport(windowId, period) : api.memberReport(scope, windowId, period))
			.then((r) => !stale && ((report = r), (error = '')))
			.catch((e) => !stale && (error = errorMessage(e)));
		return () => (stale = true);
	});

	const arrow = (trend: string) =>
		/rise|rising|up|increas/i.test(trend) ? '↗' : /fall|down|decreas/i.test(trend) ? '↘' : /stable/i.test(trend) ? '→' : '';
</script>

<div class="space-y-5">
	<div class="flex flex-wrap items-center gap-4 print:hidden">
		<div class="flex overflow-hidden rounded-xl border border-line" role="group" aria-label="Window">
			{#each WINDOWS as w (w.id)}
				<button
					type="button"
					aria-pressed={w.id === windowId}
					onclick={() => (windowId = w.id)}
					class="px-4 py-2 text-sm font-medium {w.id === windowId ? 'bg-raised text-ink' : 'bg-surface text-muted'}"
				>
					{w.label}
				</button>
			{/each}
		</div>

		<label class="flex items-center gap-2 text-sm text-muted">
			Period
			<select class={field + ' text-ink'} bind:value={period}>
				{#each periods as p (p.number)}
					<option value={p.number}>Days {p.startDay}-{p.endDay}{p.complete ? '' : ' (current)'}</option>
				{/each}
			</select>
		</label>

		<label class="flex items-center gap-2 text-sm text-muted">
			Crew
			<select class={field + ' text-ink'} bind:value={scope}>
				<option value="crew">Whole crew</option>
				{#each crew as c (c.id)}<option value={c.id}>{c.name}</option>{/each}
			</select>
		</label>

		<span class="flex-1"></span>
		{#if report}<span class="text-sm text-muted">Ground view · data as of {report.dataAsOfMet}</span>{/if}
		<Button variant="secondary" onclick={() => window.print()}>Print</Button>
	</div>

	{#if report}
		{#if report.adaptationWindow.overlaps && report.adaptationWindow.note}
			<p class="rounded-xl border border-line bg-raised/60 px-4 py-3 text-sm">{report.adaptationWindow.note}</p>
		{/if}

		{#if report.highlights.length}
			<ul class="list-disc space-y-1 pl-6">
				{#each report.highlights as h, i (i)}<li>{h}</li>{/each}
			</ul>
		{/if}

		{#each report.members as member (member.crewMemberId)}
			<section class="space-y-2 break-inside-avoid" aria-label={member.name}>
				{#if report.members.length > 1}
					<h2 class="font-display text-xl font-semibold">{member.name} <span class="text-sm font-normal text-muted">{member.role}</span></h2>
				{/if}
				<div class="max-h-[70vh] overflow-auto rounded-2xl border border-line bg-surface">
					<table class="w-full text-left text-sm">
						<thead class="sticky top-0 z-10 bg-surface text-muted shadow-[0_1px_0_var(--color-line)]">
							<tr class="border-b border-line">
								<th scope="col" class="px-4 py-3 font-medium">Metric</th>
								<th scope="col" class="px-4 py-3 font-medium">Window mean (n)</th>
								<th scope="col" class="px-4 py-3 font-medium">Baseline (n)</th>
								<th scope="col" class="px-4 py-3 font-medium">Change</th>
								<th scope="col" class="px-4 py-3 font-medium">SD from baseline</th>
								<th scope="col" class="px-4 py-3 font-medium">Trend</th>
								<th scope="col" class="px-4 py-3 font-medium">Missed</th>
							</tr>
						</thead>
						<tbody>
							{#each member.rows as r (r.metricCode)}
								<tr class="border-b border-line last:border-0">
									<th scope="row" class="px-4 py-3 font-semibold">{r.metricName}</th>
									<td class="px-4 py-3">{fmt(r.windowMean)} {r.unit} ({r.windowN})</td>
									<td class="px-4 py-3">{fmt(r.baselineMean)} ({r.baselineN ?? '–'})</td>
									<td class="px-4 py-3">{signed(r.delta)}</td>
									<td class="px-4 py-3">
										<span class="font-display text-lg font-bold">{signed(r.zSd)}</span>
										{#if r.lowConfidence}
											<span class="ml-1 rounded border border-watch px-1.5 py-0.5 text-xs text-watch">low confidence</span>
										{/if}
									</td>
									<td class="px-4 py-3">{arrow(r.trend)} {r.trend}</td>
									<td class="px-4 py-3">{r.missed} / {r.expected}</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
			</section>
		{/each}

		<div class="flex flex-wrap gap-3 text-sm">
			<span class="rounded-xl border border-line bg-surface px-3 py-2">
				Alerts: {report.alertsTotal.measurementAlerts} readings, {report.alertsTotal.symptomAlerts} symptom
			</span>
			<span class="rounded-xl border border-line bg-surface px-3 py-2">
				With context factors: {report.alertsTotal.withContextFactors}
			</span>
			<span class="rounded-xl border bg-surface px-3 py-2 {report.alertsTotal.unexplainedAlertIds.length ? 'border-act' : 'border-line'}">
				Unexplained: {report.alertsTotal.unexplainedAlertIds.length}
			</span>
		</div>
	{:else if !error}
		<p class="text-muted">Loading report…</p>
	{/if}

	{#if error}<ErrorBox message={error} onretry={() => retry++} />{/if}
</div>
