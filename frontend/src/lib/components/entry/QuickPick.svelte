<script lang="ts">
	import type { Metric } from '#lib/api/types.ts';
	import { parseNumber, stepped } from '#lib/metrics.ts';
	import Icon from '../Icon.svelte';
	import NumberStepper from './NumberStepper.svelte';

	type Props = {
		metric: Metric;
		value?: string;
		id: string;
		picks: number[];
		step: number;
		format: (v: number) => string;
		/**
		 * inline: a stepper under the chips for exact values (sleep).
		 * other: an "Other" chip that opens a stepper with a number field (exercise).
		 */
		mode: 'inline' | 'other';
		labelledby: string;
		invalid?: boolean;
	};
	let { metric, value = $bindable(), id, picks, step, format, mode, labelledby, invalid = false }: Props = $props();

	const current = $derived(parseNumber(value));
	let other = $state(false);
	const otherActive = $derived(other || (current != null && !picks.includes(current)));

	const chip = (on: boolean) =>
		`min-h-16 rounded-2xl border px-5 font-display text-2xl font-semibold tabular-nums transition active:scale-[0.97]
		${on ? 'border-action bg-action text-on-action' : 'border-line bg-surface hover:bg-raised'}`;
	const btn =
		'grid size-20 shrink-0 place-items-center rounded-2xl border border-line bg-surface transition hover:bg-raised active:scale-[0.97] active:bg-raised';
</script>

<div class="space-y-4">
	<div class="flex flex-wrap gap-3" role="group" aria-labelledby={labelledby}>
		{#each picks as p (p)}
			<button
				type="button"
				aria-pressed={current === p && !other}
				class={chip(current === p && !other)}
				onclick={() => ((value = String(p)), (other = false))}
			>
				{format(p)}
			</button>
		{/each}
		{#if mode === 'other'}
			<button type="button" aria-pressed={otherActive} class={chip(otherActive)} onclick={() => (other = true)}>
				Other
			</button>
		{/if}
	</div>

	{#if mode === 'inline'}
		<div class="flex items-center gap-4">
			<button type="button" aria-label="Decrease {metric.name} by {step * 60} minutes" class={btn} onclick={() => ((value = stepped(metric, value, -1, step)), (other = false))}>
				<Icon name="minus" class="size-8" />
			</button>
			<p
				{id}
				aria-live="polite"
				class="min-w-0 flex-1 rounded-2xl border-2 bg-surface px-5 py-4 text-center font-display text-5xl font-semibold tabular-nums
					{invalid ? 'border-watch' : 'border-line'} {current == null ? 'text-muted' : ''}"
			>
				{#if current == null}<span class="text-3xl">Not set</span>{:else}{format(current)}{/if}
			</p>
			<button type="button" aria-label="Increase {metric.name} by {step * 60} minutes" class={btn} onclick={() => ((value = stepped(metric, value, 1, step)), (other = false))}>
				<Icon name="plus" class="size-8" />
			</button>
		</div>
	{:else if otherActive}
		<NumberStepper {metric} bind:value {id} {step} inputmode="numeric" {invalid} label="{metric.name}, other value" />
	{:else}
		<p class="font-display text-5xl font-semibold tabular-nums {current == null ? 'text-muted' : ''}" aria-live="polite">
			{#if current == null}<span class="text-3xl">Not set</span>{:else}{format(current)}{/if}
		</p>
	{/if}
</div>
