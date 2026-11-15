<script lang="ts">
	import { goto } from '$app/navigation';
	import { page } from '$app/state';
	import { onMount } from 'svelte';
	import {
		ApiException, api, errorMessage, type Metric
	} from '#lib/api/index.ts';
	import { shownLevel } from '#lib/format.ts';
	import { groupMetrics, isMood, roundTo, startValue, stepFor, type MetricGroup } from '#lib/metrics.ts';
	import { renderMarkdown } from '#lib/markdown.ts';
	import { flow, type Outcome } from '#lib/stores/flow.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import Button from '#lib/components/Button.svelte';
	import Icon from '#lib/components/Icon.svelte';

	let groups = $state<MetricGroup[]>([]);
	let groupId = $state('');
	let values = $state<Record<string, string>>({});
	let saved = $state<Record<string, Outcome>>({});
	let howto = $state('');
	let implausible = $state('');
	let error = $state('');
	let submitting = $state(false);

	const group = $derived(groups.find((g) => g.id === groupId));

	const parse = (text: string | undefined) => {
		const n = Number((text ?? '').replace(',', '.'));
		return text?.trim() && Number.isFinite(n) ? n : null;
	};
	const ready = $derived(!!group && group.metrics.every((m) => parse(values[m.code]) != null));

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

	function bump(m: Metric, direction: 1 | -1) {
		const step = stepFor(m.code);
		const current = parse(values[m.code]);
		const next = current == null ? startValue(m) : current + direction * step;
		values[m.code] = String(Number(roundTo(next, step).toFixed(2)));
	}

	async function save(confirmImplausible = false) {
		if (!group) return;
		submitting = true;
		error = '';
		try {
			// BP is two readings, sent as two POSTs. Ones that already went through are not resent.
			for (const metric of group.metrics) {
				if (saved[metric.code]) continue;
				const value = parse(values[metric.code])!;
				const response = await api.submitMeasurement(session.crewId, metric.code, value, confirmImplausible);
				saved[metric.code] = { metric, value, response };
			}
			finish();
		} catch (e) {
			if (e instanceof ApiException && e.code === 'IMPLAUSIBLE_VALUE') implausible = e.message;
			else error = errorMessage(e);
		} finally {
			submitting = false;
		}
	}

	function retake() {
		for (const m of group?.metrics ?? []) if (!saved[m.code]) values[m.code] = '';
		implausible = '';
	}

	async function finish() {
		const outcomes = group!.metrics.map((m) => saved[m.code]);
		const red = outcomes.find(
			({ response }) => response.protocolId || shownLevel(response) === 'Red'
		);
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
	<div class="flex flex-wrap gap-2" role="group" aria-label="Metric">
		{#each groups as g (g.id)}
			<button
				type="button"
				aria-pressed={g.id === groupId}
				onclick={() => select(g.id)}
				class="min-h-14 rounded-full border px-6 text-lg transition
					{g.id === groupId ? 'border-action bg-raised font-semibold' : 'border-line bg-surface text-muted hover:text-ink'}"
			>
				{g.label}
			</button>
		{/each}
	</div>

	{#if group}
		<div class="grid gap-6 lg:grid-cols-[1fr_22rem]">
			<section class="flex flex-col gap-5">
				{#each group.metrics as m (m.code)}
					<div>
						<label for="v-{m.code}" class="mb-2 block text-base text-muted">
							{m.name}{m.unit ? `, ${m.unit}` : ''}{isMood(m.code) ? ': 1 (worst) to 5 (best)' : ''}
						</label>

						{#if isMood(m.code)}
							<div id="v-{m.code}" class="grid grid-cols-5 gap-3" role="group">
								{#each [1, 2, 3, 4, 5] as n (n)}
									<button
										type="button"
										aria-pressed={values[m.code] === String(n)}
										onclick={() => (values[m.code] = String(n))}
										class="min-h-18 rounded-2xl border font-display text-3xl font-semibold
											{values[m.code] === String(n) ? 'border-action bg-action text-on-action' : 'border-line bg-surface'}"
									>
										{n}
									</button>
								{/each}
							</div>
						{:else}
							<div class="flex gap-3">
								<button
									type="button"
									aria-label="Decrease {m.name}"
									onclick={() => bump(m, -1)}
									class="grid size-20 shrink-0 place-items-center rounded-2xl border border-line bg-surface hover:bg-raised"
								>
									<Icon name="minus" class="size-8" />
								</button>
								<input
									id="v-{m.code}"
									inputmode="decimal"
									autocomplete="off"
									bind:value={values[m.code]}
									class="h-20 min-w-0 flex-1 rounded-2xl border-2 border-line bg-surface px-5 text-right font-display text-6xl font-semibold focus:border-action focus:outline-none"
								/>
								<button
									type="button"
									aria-label="Increase {m.name}"
									onclick={() => bump(m, 1)}
									class="grid size-20 shrink-0 place-items-center rounded-2xl border border-line bg-surface hover:bg-raised"
								>
									<Icon name="plus" class="size-8" />
								</button>
							</div>
						{/if}
					</div>
				{/each}

				{#if implausible}
					<div role="alert" class="rounded-2xl border border-watch bg-watch-surface p-4">
						<p class="flex items-center gap-2 font-semibold text-watch">
							<Icon name="triangle" /> Check this value
						</p>
						<p class="mt-2">{implausible}</p>
						<div class="mt-4 grid grid-cols-2 gap-3">
							<Button onclick={retake}>Retake</Button>
							<Button variant="watch" disabled={submitting} onclick={() => save(true)}>Save anyway</Button>
						</div>
					</div>
				{/if}

				{#if error}<p role="alert" class="rounded-xl border border-act bg-act-surface p-4 text-act-ink">{error}</p>{/if}

				<Button size="lg" class="mt-auto" disabled={!ready || submitting || !!implausible} onclick={() => save()}>
					{submitting ? 'Saving…' : 'Save reading'}
				</Button>
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
