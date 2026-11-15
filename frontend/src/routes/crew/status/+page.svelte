<script lang="ts">
	import { goto } from '$app/navigation';
	import { onDestroy, onMount } from 'svelte';
	import { LEVEL_WORD, fmt, ruleLabel, shownLevel, toLevel, worst } from '#lib/format.ts';
	import { statusSourceLines } from '#lib/metrics.ts';
	import { flow } from '#lib/stores/flow.svelte.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import { HEART_CODES, LUNG_CODES, chestLevels, latestReadings, levelsOf, type ChestLevels } from '#lib/chest.ts';
	import ChestFigure from '#lib/components/ChestFigure.svelte';
	import Button from '#lib/components/Button.svelte';
	import Icon from '#lib/components/Icon.svelte';
	import StatusBadge from '#lib/components/StatusBadge.svelte';

	let index = $state(0);
	/** "Retake in 10 min" ends at this mission time (ms); fast-forward shortens it. */
	let retakeAt = $state<number | null>(null);
	let tick = $state(0);
	let timer: ReturnType<typeof setInterval> | undefined;
	const retakeIn = $derived.by(() => {
		void tick; // re-evaluated by the 1 s timer below
		const now = mission.nowMs();
		if (retakeAt == null || now == null) return null;
		return Math.max(0, Math.ceil((retakeAt - now) / 1000));
	});

	const current = $derived(flow.outcomes[index]);
	const response = $derived(current?.response);
	const level = $derived((response && shownLevel(response)) ?? 'Green');
	const overall = $derived(worst(flow.outcomes.map((o) => shownLevel(o.response))) ?? 'Green');
	const protocolId = $derived(flow.outcomes.find((o) => o.response.protocolId)?.response.protocolId);
	const packetDraft = $derived(flow.outcomes.find((o) => o.response.packetDraft)?.response.packetDraft);

	// Plain words for status.rule on a crew screen (ui-spec: no jargon such as "zscore").
	const RULE_TEXT: Record<string, string> = {
		none: 'Checked against: fixed limits and your personal baseline',
		threshold: 'Flagged by: a fixed limit',
		zscore: 'Flagged by: your personal baseline',
		'threshold+zscore': 'Flagged by: a fixed limit and your personal baseline'
	};
	const ruleText = $derived.by(() => {
		const s = response?.status;
		if (!s) return '';
		const text = RULE_TEXT[s.rule ?? 'none'] ?? RULE_TEXT.none;
		return s.baselineN != null ? `${text} (n=${s.baselineN})` : text;
	});
	const sourceLines = $derived(current && response ? statusSourceLines(current.metric, response.status) : []);
	const original = $derived(response?.context ? toLevel(response.context.originalLevel) : null);
	const loweredBy = $derived.by(() => {
		const ctx = response?.context;
		if (!ctx?.downgraded) return [];
		const ids = ctx.downgradedBy?.length
			? ctx.downgradedBy
			: ctx.factors.filter((f) => Number(f.severityModifier) === -1).map((f) => f.ruleId);
		return ids.map(ruleLabel);
	});
	const lowered = $derived(!!response?.context?.downgraded && !!original);
	const loweredNote = $derived(lowered ? `Lowered from ${LEVEL_WORD[original!]} by ${loweredBy.join(', ')}` : undefined);

	// Chest figure: latest stored levels, overridden by the readings just saved (their shown level
	// includes a context downgrade, which the history endpoint does not carry).
	let chest = $state<ChestLevels | null>(null);

	onMount(() => {
		if (!flow.outcomes.length) {
			goto('/crew');
			return;
		}
		latestReadings(session.crewId, [...HEART_CODES, ...LUNG_CODES])
			.then((latest) => {
				const levels = levelsOf(latest);
				for (const o of flow.outcomes) levels[o.metric.code] = shownLevel(o.response);
				chest = chestLevels(levels);
			})
			.catch(() => {});
	});
	onDestroy(() => clearInterval(timer));

	function startRetakeTimer() {
		const now = mission.nowMs();
		if (now == null) return;
		retakeAt = now + 600_000;
		clearInterval(timer);
		timer = setInterval(() => {
			tick++;
			if (retakeIn === 0) clearInterval(timer);
		}, 1000);
	}

	const clock = (s: number) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;

	function done() {
		const target = flow.returnTo ?? '/crew';
		flow.returnTo = null;
		goto(target);
	}

	function reviewPacket() {
		flow.packet = packetDraft ?? null;
		goto('/crew/packet');
	}
</script>

<svelte:head>
	<title>{current ? `${LEVEL_WORD[level]} · ${current.metric.name} ${fmt(current.value)} ${current.metric.unit}` : 'Result'} · BASELINE crew</title>
</svelte:head>

{#if current && response}
	<h1 class="sr-only">Result: {current.metric.name}, {LEVEL_WORD[level]}</h1>
	<div class="grid gap-6 lg:grid-cols-[22rem_1fr]">
		<section class="flex flex-col gap-4 rounded-2xl border border-line bg-surface p-5" aria-label="Result">
			{#if flow.outcomes.length > 1}
				<div class="grid grid-cols-2 gap-2" role="group" aria-label="Reading">
					{#each flow.outcomes as o, i (o.metric.code)}
						<button
							type="button"
							aria-pressed={i === index}
							onclick={() => (index = i)}
							class="min-h-11 rounded-xl border px-3 text-sm {i === index ? 'border-action bg-raised font-semibold' : 'border-line text-muted'}"
						>
							{o.metric.name}
						</button>
					{/each}
				</div>
			{/if}

			<p class="text-base text-muted">{current.metric.name}</p>
			<p class="font-display text-[88px] leading-none font-bold">
				{fmt(current.value)}<span class="ml-3 text-2xl font-medium text-muted">{current.metric.unit}</span>
			</p>

			<StatusBadge {level} size="lg" note={loweredNote} />

			{#if lowered}
				<p class="flex items-start gap-2 text-sm text-watch">
					<Icon name="triangle" class="mt-0.5 size-4 shrink-0" />
					Original flag: {LEVEL_WORD[original!]} · still logged and sent to ground
				</p>
			{/if}

			{#if response.status.reason}<p class="text-lg">{response.status.reason}</p>{/if}
			{#each sourceLines as line (line)}<p class="text-sm text-muted">{line}</p>{/each}
			<p class="mt-auto text-sm text-muted">{ruleText}</p>
		</section>

		<section class="flex flex-col gap-4" aria-labelledby="why">
			<h2 id="why" class="font-display text-2xl font-semibold">Why it may have moved</h2>

			{#each response.context?.factors ?? [] as f (f.ruleId)}
				<article class="rounded-2xl border border-line bg-surface p-4">
					<h3 class="text-lg font-semibold">
						{ruleLabel(f.ruleId)} · <span class="font-normal text-muted">{Number(f.severityModifier) === -1 ? 'lowers the flag' : 'shown only'}</span>
					</h3>
					<p class="mt-1 text-base">{f.message}</p>
					<p class="mt-2 text-sm text-muted">
						Source: {f.sources.map((s) => s.publisher).filter(Boolean).join(' · ') || 'none listed'}
					</p>
				</article>
			{:else}
				<p class="rounded-2xl border border-line bg-surface p-4 text-muted">
					No known factor found. Ground will see this as unexplained.
				</p>
			{/each}

			{#if chest}
				<div class="rounded-2xl border border-line bg-surface p-4">
					<ChestFigure heart={chest.heart} lungs={chest.lungs} heightClass="h-[120px] lg:h-[170px]" layout="side" />
				</div>
			{/if}

			<div class="mt-auto flex gap-4 pt-4">
				{#if overall === 'Red'}
					{#if protocolId}
						<Button href="/crew/protocol" variant="danger" size="lg" class="flex-1">Open protocol</Button>
					{:else}
						<Button variant="danger" size="lg" class="flex-1" onclick={reviewPacket}>Review packet</Button>
					{/if}
				{:else}
					<Button size="lg" class="flex-1" onclick={done}>{flow.returnTo ? 'Back to protocol' : 'Done'}</Button>
				{/if}

				{#if overall === 'Yellow'}
					<Button
						variant="secondary"
						size="lg"
						disabled={retakeIn != null && retakeIn > 0}
						onclick={() => (retakeIn === 0 ? goto(`/crew/measure?metric=${current.metric.code}`) : startRetakeTimer())}
					>
						{retakeIn == null ? 'Retake in 10 min' : retakeIn > 0 ? `Retake in ${clock(retakeIn)}` : 'Retake now'}
					</Button>
				{:else}
					<Button href="/crew/trends?metric={current.metric.code}" variant="secondary" size="lg">View trend</Button>
				{/if}
			</div>
		</section>
	</div>
{/if}
