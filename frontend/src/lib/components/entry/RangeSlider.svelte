<script lang="ts">
	import type { Metric } from '#lib/api/types.ts';
	import { parseNumber, startValue, stepFor, stepped } from '#lib/metrics.ts';
	import Icon from '../Icon.svelte';

	type Props = { metric: Metric; value?: string; id: string; invalid?: boolean; describedby?: string };
	let { metric, value = $bindable(), id, invalid = false, describedby }: Props = $props();

	const step = $derived(stepFor(metric.code));
	const min = $derived(metric.plausibleMin);
	const max = $derived(metric.plausibleMax);
	const current = $derived(parseNumber(value));
	// Until touched the thumb rests at the start value, but nothing is selected (Save stays disabled).
	const position = $derived(current ?? Math.round(startValue(metric)));

	// Tick labels every 10 units across the plausible range from config, ends included.
	const ticks = $derived.by(() => {
		const out = [min];
		for (let t = Math.ceil(min / 10) * 10; t < max; t += 10) if (t > min) out.push(t);
		out.push(max);
		return out;
	});
	const pct = (v: number) => ((v - min) / (max - min)) * 100;

	const btn =
		'grid size-20 shrink-0 place-items-center rounded-2xl border border-line bg-surface transition hover:bg-raised active:scale-[0.97] active:bg-raised';
</script>

<div class="space-y-4">
	<p class="font-display text-7xl leading-none font-semibold tabular-nums {current == null ? 'text-muted' : ''}" aria-hidden="true">
		{#if current == null}<span class="text-3xl font-medium">Not set</span>{:else}{current}<span class="ml-3 text-2xl font-medium text-muted">{metric.unit}</span>{/if}
	</p>
	<div class="flex items-center gap-4">
		<button type="button" aria-label="Decrease {metric.name} by {step}" class={btn} onclick={() => (value = stepped(metric, value, -1))}>
			<Icon name="minus" class="size-8" />
		</button>
		<div class="min-w-0 flex-1">
			<input
				{id}
				type="range"
				{min}
				{max}
				{step}
				value={position}
				aria-describedby={describedby}
				aria-invalid={invalid || undefined}
				aria-valuetext={current == null ? 'Not set' : `${current} ${metric.unit}`}
				oninput={(e) => (value = (e.currentTarget as HTMLInputElement).value)}
				class="slider w-full {current == null ? 'untouched' : ''} {invalid ? 'invalid' : ''}"
			/>
			<div class="relative mt-2 h-5 text-sm text-muted tabular-nums" aria-hidden="true">
				{#each ticks as t (t)}
					<span class="absolute -translate-x-1/2" style="left: {pct(t)}%">{t}</span>
				{/each}
			</div>
		</div>
		<button type="button" aria-label="Increase {metric.name} by {step}" class={btn} onclick={() => (value = stepped(metric, value, 1))}>
			<Icon name="plus" class="size-8" />
		</button>
	</div>
</div>

<style>
	/* Large track and a 56 px thumb, usable with gloves. */
	.slider {
		appearance: none;
		height: 56px;
		background: transparent;
		cursor: pointer;
	}
	.slider::-webkit-slider-runnable-track {
		height: 16px;
		border-radius: 999px;
		background: var(--color-raised);
		border: 1px solid var(--color-line);
	}
	.slider::-moz-range-track {
		height: 16px;
		border-radius: 999px;
		background: var(--color-raised);
		border: 1px solid var(--color-line);
	}
	.slider::-webkit-slider-thumb {
		appearance: none;
		width: 56px;
		height: 56px;
		margin-top: -21px;
		border-radius: 999px;
		background: var(--color-action);
		border: 4px solid var(--color-bg);
	}
	.slider::-moz-range-thumb {
		width: 48px;
		height: 48px;
		border-radius: 999px;
		background: var(--color-action);
		border: 4px solid var(--color-bg);
	}
	.slider.untouched::-webkit-slider-thumb {
		background: var(--color-muted);
	}
	.slider.untouched::-moz-range-thumb {
		background: var(--color-muted);
	}
	.slider.invalid::-webkit-slider-runnable-track {
		border-color: var(--color-watch);
	}
	.slider:focus-visible {
		outline: 2px solid var(--color-action);
		outline-offset: 4px;
		border-radius: 999px;
	}
</style>
