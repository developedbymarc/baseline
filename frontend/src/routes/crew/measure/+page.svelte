<script lang="ts">
	import { goto } from '$app/navigation';
	import { page } from '$app/state';
	import { onMount } from 'svelte';
	import { ApiException, api, errorMessage, type Metric } from '#lib/api/index.ts';
	import { shownLevel } from '#lib/format.ts';
	import {
		EXERCISE_PICKS, EXERCISE_STEP, GROUP_ICON, MOOD_WORDS, SLEEP_PICKS,
		entryKind, formatHours, groupMetrics, parseNumber, stepFor, type MetricGroup
	} from '#lib/metrics.ts';
	import { renderMarkdown } from '#lib/markdown.ts';
	import { flow, type Outcome } from '#lib/stores/flow.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import Button from '#lib/components/Button.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import NumberStepper from '#lib/components/entry/NumberStepper.svelte';
	import QuickPick from '#lib/components/entry/QuickPick.svelte';
	import RangeSlider from '#lib/components/entry/RangeSlider.svelte';

	let groups = $state<MetricGroup[]>([]);
	let groupId = $state('');
	let values = $state<Record<string, string>>({});
	let saved = $state<Record<string, Outcome>>({});
	let howto = $state('');
	let implausible = $state('');
	/** Metric whose POST returned 422, so its field can be marked. */
	let failedCode = $state<string | null>(null);
	let error = $state('');
	let submitting = $state(false);

	const group = $derived(groups.find((g) => g.id === groupId));
	const isBp = $derived(group?.id === 'bp' && group.metrics.length === 2);
	const isMoodGroup = $derived(!!group && group.metrics.every((m) => entryKind(m.code) === 'mood'));
	const failedName = $derived(group && group.metrics.length > 1 ? group.metrics.find((m) => m.code === failedCode)?.name : null);
	const ready = $derived(!!group && group.metrics.every((m) => parseNumber(values[m.code]) != null));

	onMount(async () => {
		try {
			groups = groupMetrics(await api.metrics());
			const wanted = page.url.searchParams.get('metric');
			groupId = (groups.find((g) => g.metrics.some((m) => m.code === wanted)) ?? groups[0])?.id ?? '';
		} catch (e) {
			error = errorMessage(e);
		}
	});

	function select(id: string) {
		groupId = id;
		values = {};
		saved = {};
		implausible = error = '';
		failedCode = null;
	}

	// How-to for the selected metric. The API sends a placeholder when the file is missing; show it as is.
	$effect(() => {
		const code = group?.metrics[0].code;
		howto = '';
		if (!code) return;
		let stale = false;
		api
			.howto(code)
			.then((h) => !stale && (howto = renderMarkdown(h.markdown)))
			.catch(() => {});
		return () => (stale = true);
	});

	async function save(confirmImplausible = false) {
		if (!group || submitting) return;
		submitting = true;
		error = '';
		try {
			// BP is two readings, sent as two POSTs. Ones that already went through are not resent.
			for (const metric of group.metrics) {
				if (saved[metric.code]) continue;
				failedCode = metric.code;
				const value = parseNumber(values[metric.code])!;
				const response = await api.submitMeasurement(session.crewId, metric.code, value, confirmImplausible);
				saved[metric.code] = { metric, value, response };
			}
			failedCode = null;
			await finish();
		} catch (e) {
			if (e instanceof ApiException && e.code === 'IMPLAUSIBLE_VALUE') implausible = e.message;
			else {
				failedCode = null;
				error = errorMessage(e);
			}
		} finally {
			submitting = false;
		}
	}

	function retake() {
		for (const m of group?.metrics ?? []) if (!saved[m.code]) values[m.code] = '';
		implausible = '';
		failedCode = null;
	}

	/** Mood: one tap saves (no separate Save button). */
	function pickMood(m: Metric, n: number) {
		if (submitting) return;
		values[m.code] = String(n);
		save();
	}

	async function finish() {
		const outcomes = group!.metrics.map((m) => saved[m.code]);
		const red = outcomes.find(({ response }) => response.protocolId || shownLevel(response) === 'Red');
		if (red) {
			const { metric, value, response } = red;
			// Same wording as the stored alert: "Resting heart rate 130 bpm: Above the critical limit …"
			const reading = `${metric.name} ${value}${metric.unit ? ` ${metric.unit}` : ''}`;
			flow.openAlert({
				alertId: response.alertId ?? null,
				reason: response.status.reason ? `${reading}: ${response.status.reason}` : `${reading}: red reading.`,
				protocolId: response.protocolId ?? null
			});
		}
		flow.outcomes = outcomes;
		await goto('/crew/status');
	}
</script>

<div class="space-y-6">
	<!-- Metric picker: large segmented tabs, icon + name. -->
	<div class="grid grid-cols-7 gap-2 rounded-2xl border border-line bg-surface p-2" role="group" aria-label="Metric">
		{#each groups as g (g.id)}
			<button
				type="button"
				aria-pressed={g.id === groupId}
				onclick={() => select(g.id)}
				class="flex min-h-18 flex-col items-center justify-center gap-1 rounded-xl px-1 py-2 text-center text-base leading-tight transition active:bg-raised
					{g.id === groupId ? 'bg-raised font-semibold text-ink shadow-[inset_0_-4px_0_var(--color-action)]' : 'text-muted hover:text-ink'}"
			>
				<Icon name={GROUP_ICON[g.id] ?? 'activity'} class="size-7" />
				{g.label}
			</button>
		{/each}
	</div>

	{#if group}
		<div class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
			<section class="flex min-w-0 flex-col gap-5" aria-label="{group.label} entry">
				{#if isBp}
					{@const [sys, dia] = group.metrics}
					<!-- One composite control: SYS / DIA mmHg, sent as two POSTs. -->
					<div>
						<p id="bp-label" class="mb-2 text-base text-muted">
							Blood pressure: <span class="font-display text-xl text-ink">SYS / DIA</span> {sys.unit}
						</p>
						<div class="grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-start gap-3" role="group" aria-labelledby="bp-label">
							<div class="min-w-0 space-y-1">
								<label for="v-{sys.code}" class="block text-sm font-semibold text-muted">SYS (top number)</label>
								<NumberStepper metric={sys} bind:value={values[sys.code]} id="v-{sys.code}" layout="stack" inputmode="numeric" invalid={failedCode === sys.code && !!implausible} />
							</div>
							<span class="pt-10 font-display text-6xl text-muted" aria-hidden="true">/</span>
							<div class="min-w-0 space-y-1">
								<label for="v-{dia.code}" class="block text-sm font-semibold text-muted">DIA (bottom number)</label>
								<NumberStepper metric={dia} bind:value={values[dia.code]} id="v-{dia.code}" layout="stack" inputmode="numeric" invalid={failedCode === dia.code && !!implausible} />
							</div>
						</div>
					</div>
				{:else}
					{#each group.metrics as m (m.code)}
						{@const kind = entryKind(m.code)}
						{@const invalid = failedCode === m.code && !!implausible}
						<div>
							{#if kind === 'mood'}
								<p id="label-{m.code}" class="mb-2 text-base text-muted">{m.name}: tap one to save</p>
								<div class="grid grid-cols-5 gap-3" role="group" aria-labelledby="label-{m.code}">
									{#each MOOD_WORDS as word, i (word)}
										{@const n = i + 1}
										<button
											type="button"
											aria-pressed={values[m.code] === String(n)}
											disabled={submitting}
											onclick={() => pickMood(m, n)}
											class="flex min-h-28 flex-col items-center justify-center gap-1 rounded-2xl border transition active:scale-[0.97] disabled:opacity-60
												{values[m.code] === String(n) ? 'border-action bg-action text-on-action' : 'border-line bg-surface hover:bg-raised'}"
										>
											<span class="font-display text-4xl font-semibold">{n}</span>
											<span class="text-base">{word}</span>
										</button>
									{/each}
								</div>
								<p class="mt-2 text-sm text-muted">{submitting ? 'Saving…' : '1 = worst, 5 = best'}</p>
							{:else if kind === 'slider'}
								<label for="v-{m.code}" class="mb-2 block text-base text-muted">{m.name}</label>
								<RangeSlider metric={m} bind:value={values[m.code]} id="v-{m.code}" {invalid} />
							{:else if kind === 'sleep'}
								<p id="label-{m.code}" class="mb-2 text-base text-muted">{m.name}</p>
								<QuickPick metric={m} bind:value={values[m.code]} id="v-{m.code}" picks={SLEEP_PICKS} step={stepFor(m.code)} format={formatHours} mode="inline" labelledby="label-{m.code}" {invalid} />
							{:else if kind === 'exercise'}
								<p id="label-{m.code}" class="mb-2 text-base text-muted">{m.name}</p>
								<QuickPick metric={m} bind:value={values[m.code]} id="v-{m.code}" picks={EXERCISE_PICKS} step={EXERCISE_STEP} format={(v) => `${v} ${m.unit}`} mode="other" labelledby="label-{m.code}" {invalid} />
							{:else}
								{@const isTemp = m.code.startsWith('temp')}
								<label for="v-{m.code}" class="block text-base text-muted">{m.name}</label>
								{#if isTemp && m.unit}
									<!-- Guards against Fahrenheit entries (98.6). -->
									<p id="hint-{m.code}" class="text-sm font-semibold text-watch">in {m.unit}</p>
								{/if}
								<div class="mt-2">
									<NumberStepper
										metric={m}
										bind:value={values[m.code]}
										id="v-{m.code}"
										inputmode={Number.isInteger(stepFor(m.code)) ? 'numeric' : 'decimal'}
										describedby={isTemp ? `hint-${m.code}` : undefined}
										{invalid}
									/>
								</div>
							{/if}
						</div>
					{/each}
				{/if}

				{#if implausible}
					<div role="alert" class="rounded-2xl border border-watch bg-watch-surface p-4">
						<p class="flex items-center gap-2 font-semibold text-watch">
							<Icon name="triangle" /> Check this value{failedName ? `: ${failedName}` : ''}
						</p>
						<p class="mt-2">{implausible}</p>
						<div class="mt-4 grid grid-cols-2 gap-3">
							<Button onclick={retake}>Retake</Button>
							<Button variant="watch" disabled={submitting} onclick={() => save(true)}>Save anyway</Button>
						</div>
					</div>
				{/if}

				{#if error}<p role="alert" class="rounded-xl border border-act bg-act-surface p-4 text-act-ink">{error}</p>{/if}

				{#if !isMoodGroup}
					<Button size="lg" disabled={!ready || submitting || !!implausible} onclick={() => save()}>
						{submitting ? 'Saving…' : 'Save reading'}
					</Button>
				{/if}
			</section>

			<aside class="rounded-2xl border border-line bg-surface p-5" aria-labelledby="howto">
				<h2 id="howto" class="font-display text-2xl font-semibold">How to take it</h2>
				<div class="mt-4 space-y-3 text-base leading-relaxed">
					<!-- eslint-disable-next-line svelte/no-at-html-tags -- renderMarkdown escapes the source -->
					{@html howto}
				</div>
			</aside>
		</div>
	{:else if error}
		<p role="alert" class="rounded-xl border border-act bg-act-surface p-4 text-act-ink">{error}</p>
	{/if}
</div>
