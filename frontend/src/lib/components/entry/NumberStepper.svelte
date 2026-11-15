<script lang="ts">
	import type { Metric } from '#lib/api/types.ts';
	import { stepFor, stepped } from '#lib/metrics.ts';
	import Icon from '../Icon.svelte';

	type Props = {
		metric: Metric;
		value?: string;
		id: string;
		/** row: [−] [value unit] [+]. stack: value on top, [−][+] below (BP halves side by side). */
		layout?: 'row' | 'stack';
		step?: number;
		/** numeric for whole numbers, decimal for e.g. temperature. */
		inputmode?: 'numeric' | 'decimal';
		/** The field that failed (422 on this metric). */
		invalid?: boolean;
		describedby?: string;
		/** Accessible name when no visible <label for> points at the input. */
		label?: string;
	};
	let {
		metric,
		value = $bindable(),
		id,
		layout = 'row',
		step = stepFor(metric.code),
		inputmode = 'decimal',
		invalid = false,
		describedby,
		label
	}: Props = $props();

	const btn =
		'grid shrink-0 place-items-center rounded-2xl border border-line bg-surface transition hover:bg-raised active:scale-[0.97] active:bg-raised';
</script>

{#snippet minus(cls: string)}
	<button
		type="button"
		aria-label="Decrease {metric.name} by {step}"
		onclick={() => (value = stepped(metric, value, -1, step))}
		class="{btn} {cls}"
	>
		<Icon name="minus" class="size-8" />
	</button>
{/snippet}
{#snippet plus(cls: string)}
	<button
		type="button"
		aria-label="Increase {metric.name} by {step}"
		onclick={() => (value = stepped(metric, value, 1, step))}
		class="{btn} {cls}"
	>
		<Icon name="plus" class="size-8" />
	</button>
{/snippet}
{#snippet field()}
	<!-- The unit is fixed text beside the number, never part of what is typed. -->
	<div
		class="flex h-24 min-w-0 flex-1 items-center rounded-2xl border-2 bg-surface focus-within:border-action
			{invalid ? 'border-watch' : 'border-line'}"
	>
		<input
			{id}
			{inputmode}
			size="1"
			autocomplete="off"
			aria-label={label}
			aria-describedby={describedby}
			aria-invalid={invalid || undefined}
			bind:value
			class="w-full min-w-0 flex-1 bg-transparent pl-4 text-right font-display text-7xl font-semibold tabular-nums placeholder:text-muted focus:outline-none"
		/>
		<span class="shrink-0 pr-5 pl-3 font-display text-2xl text-muted">{metric.unit}</span>
	</div>
{/snippet}

{#if layout === 'stack'}
	<div class="flex min-w-0 flex-1 flex-col gap-3">
		{@render field()}
		<div class="grid grid-cols-2 gap-3">
			{@render minus('h-16 w-full')}
			{@render plus('h-16 w-full')}
		</div>
	</div>
{:else}
	<div class="flex gap-3">
		{@render minus('size-24')}
		{@render field()}
		{@render plus('size-24')}
	</div>
{/if}
