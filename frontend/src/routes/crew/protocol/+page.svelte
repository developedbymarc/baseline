<script lang="ts">
	import { goto } from '$app/navigation';
	import { onMount } from 'svelte';
	import { api, errorMessage, type Metric } from '#lib/api/index.ts';
	import { flow } from '#lib/stores/flow.svelte.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import { session } from '#lib/stores/session.svelte.ts';
	import Button from '#lib/components/Button.svelte';
	import Icon from '#lib/components/Icon.svelte';

	const DEFAULT_PROTOCOL = 'chest_pain';

	let metrics = $state<Metric[]>([]);
	let error = $state('');
	let busy = $state(false);
	let secondsLeft = $state(0);

	const protocol = $derived(flow.protocol);
	const node = $derived(protocol?.treeYaml.find((n) => n.id === flow.nodeId));
	const onboardFirst = $derived(mission.state?.onboardFirst ?? false);
	const canNotify = $derived(!!node && (node.notifyGround || node.buildPacket) && node.type !== 'end');

	const sourceText = $derived(
		(node?.src ?? [])
			.map((id) => {
				if (id === 'team') return 'Team step, no external source';
				const s = protocol?.sources.find((x) => x.id === id);
				return s ? [s.publisher, s.title].filter(Boolean).join(', ') : id;
			})
			.join('; ')
	);

	onMount(async () => {
		try {
			metrics = await api.metrics();
			if (flow.run && flow.protocol) return; // coming back from a measurement
			const id = flow.alert?.protocolId ?? DEFAULT_PROTOCOL;
			const [p, run] = await Promise.all([
				api.protocol(id),
				api.startRun(session.crewId, id, flow.alert?.alertId ?? null)
			]);
			flow.protocol = p;
			flow.run = run;
			flow.nodeId = p.start;
			flow.step = 1;
		} catch (e) {
			error = errorMessage(e);
		}
	});

	// A timed step keeps Next disabled until the countdown ends.
	$effect(() => {
		const seconds = Number(node?.timerS ?? 0);
		secondsLeft = seconds;
		if (!seconds) return;
		const t = setInterval(() => {
			secondsLeft = Math.max(0, secondsLeft - 1);
			if (!secondsLeft) clearInterval(t);
		}, 1000);
		return () => clearInterval(t);
	});

	const clock = (s: number) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;

	async function advance(answer: string | null, next?: string | null) {
		if (!flow.run || !node) return;
		busy = true;
		error = '';
		try {
			await api.answerStep(flow.run.id, node.id, answer);
			if (!next) throw new Error('This step has no next step defined.');
			flow.nodeId = next;
			flow.step += 1;
		} catch (e) {
			error = errorMessage(e);
		} finally {
			busy = false;
		}
	}

	async function endRun() {
		if (!flow.run || !node) return;
		busy = true;
		error = '';
		try {
			await api.endRun(flow.run.id, node.outcome ?? null);
			if (node.notifyGround || node.buildPacket) {
				await goto('/crew/packet');
			} else {
				flow.closeAlert();
				await goto('/crew');
			}
		} catch (e) {
			error = errorMessage(e);
		} finally {
			busy = false;
		}
	}

	function measure(code: string) {
		flow.returnTo = '/crew/protocol';
		goto(`/crew/measure?metric=${code}`);
	}
</script>

{#if protocol && node}
	<div class="mx-auto flex min-h-[calc(100dvh-13rem)] max-w-4xl flex-col gap-6">
		<div class="flex justify-between text-sm text-muted">
			<p>{protocol.title} · v{protocol.version} · step {flow.step}</p>
			<p>Every step is logged with its time</p>
		</div>

		<div class="flex-1 space-y-6">
			{#if node.review}
				<p class="inline-flex items-center gap-2 rounded-lg border border-dashed border-watch px-3 py-1.5 text-sm text-watch">
					<Icon name="triangle" class="size-4" /> Needs medical review
				</p>
			{/if}
			<p class="text-4xl leading-snug font-semibold">{node.text}</p>
			{#if sourceText}<p class="text-sm text-muted">Source: {sourceText}</p>{/if}

			{#if node.type === 'measure'}
				<div class="flex flex-wrap gap-3">
					{#each node.metrics ?? [] as code (code)}
						<Button variant="secondary" onclick={() => measure(code)}>
							Measure {metrics.find((m) => m.code === code)?.name ?? code}
						</Button>
					{/each}
				</div>
			{/if}
		</div>

		{#if error}<p role="alert" class="rounded-xl border border-act bg-act-surface p-4 text-act-ink">{error}</p>{/if}

		{#if node.type === 'question'}
			<div class="grid gap-4 sm:grid-cols-2">
				{#each node.options ?? [] as option (option.label)}
					<Button size="xl" disabled={busy} onclick={() => advance(option.label, option.next)}>{option.label}</Button>
				{/each}
			</div>
		{:else}
			<div class="flex gap-4">
				{#if node.type === 'end'}
					<Button size="lg" class="flex-1" disabled={busy} onclick={endRun}>End protocol</Button>
				{:else}
					<Button
						size="lg"
						variant={canNotify && !onboardFirst ? 'secondary' : 'primary'}
						class="flex-1"
						disabled={busy || secondsLeft > 0}
						onclick={() => advance('done', node.next)}
					>
						{secondsLeft > 0 ? `Wait ${clock(secondsLeft)}` : 'Done, next'}
					</Button>
				{/if}
				{#if canNotify}
					<Button
						size="lg"
						variant={onboardFirst ? 'secondary' : 'primary'}
						onclick={() => goto('/crew/packet')}
					>
						Notify ground
					</Button>
				{/if}
			</div>
		{/if}

		{#if protocol.disclaimer}<p class="text-xs text-muted">{protocol.disclaimer}</p>{/if}
	</div>
{:else if error}
	<p role="alert" class="rounded-xl border border-act bg-act-surface p-4 text-act-ink">{error}</p>
{:else}
	<p class="text-muted">Loading protocol…</p>
{/if}
