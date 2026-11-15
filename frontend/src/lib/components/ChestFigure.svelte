<script lang="ts">
	import type { Level } from '#lib/api/types.ts';
	import { LEVEL_WORD } from '#lib/format.ts';
	import StatusBadge from './StatusBadge.svelte';

	type Props = {
		heart: Level | null;
		lungs: Level | null;
		/** Height of the drawing in px (about 260 by default). */
		size?: number;
		/** stack: labels under the drawing. side: labels to the right. */
		layout?: 'stack' | 'side';
		/** Tailwind height classes for responsive sizes; overrides `size`. */
		heightClass?: string;
	};
	let { heart, lungs, size = 260, layout = 'stack', heightClass }: Props = $props();

	// Colour comes from the status tokens; shape and word are drawn too (never colour alone).
	const STROKE = { Green: 'var(--color-ok)', Yellow: 'var(--color-watch)', Red: 'var(--color-act)' } as const;
	const FILL = { Green: 'var(--color-ok-surface)', Yellow: 'var(--color-watch-surface)', Red: 'var(--color-act-surface)' } as const;
	const stroke = (l: Level | null) => (l ? STROKE[l] : 'var(--color-line)');
	const fill = (l: Level | null) => (l ? FILL[l] : 'var(--color-surface)');
	const word = (l: Level | null) => (l ? LEVEL_WORD[l] : 'no status yet');

	const label = $derived(`Chest status. Heart: ${word(heart)}. Lungs: ${word(lungs)}.`);
</script>

<!-- Status shape inside an organ, same glyphs as StatusBadge (24-unit paths). -->
{#snippet glyph(level: Level | null, x: number, y: number)}
	{#if level}
		<g transform="translate({x - 12} {y - 12})" fill="none" stroke={stroke(level)} stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round">
			{#if level === 'Green'}
				<circle cx="12" cy="12" r="9" fill="var(--color-bg)" /><path d="m8 12 3 3 5-6" />
			{:else if level === 'Yellow'}
				<path d="M12 3 22 20H2L12 3Z" fill="var(--color-bg)" /><path d="M12 10v4M12 17v.01" />
			{:else}
				<path d="M8 2h8l6 6v8l-6 6H8l-6-6V8l6-6Z" fill="var(--color-bg)" /><path d="M12 7v6M12 16.5v.01" />
			{/if}
		</g>
	{/if}
{/snippet}

<figure class="flex gap-4 {layout === 'side' ? 'flex-row items-center' : 'flex-col items-center'}">
	<svg viewBox="0 0 200 230" style={heightClass ? undefined : `height: ${size}px`} class="w-auto shrink-0 {heightClass ?? ''}" role="img" aria-label={label}>
		<!-- Torso -->
		<path d="M30 40 Q100 8 170 40 L182 214 Q100 230 18 214 Z" fill="var(--color-surface)" stroke="var(--color-line)" stroke-width="2" />
		<!-- Trachea -->
		<path d="M100 22 V66 M100 66 L88 78 M100 66 L112 78" fill="none" stroke="var(--color-line)" stroke-width="4" stroke-linecap="round" />
		<!-- Lungs (crew member's right lung is on the viewer's left) -->
		<path d="M88 74 C64 70 44 96 42 136 C40 176 52 196 78 192 C90 190 94 172 94 140 L94 82 Z" fill={fill(lungs)} stroke={stroke(lungs)} stroke-width="3" />
		<path d="M112 74 C136 70 156 96 158 136 C160 176 148 196 122 192 C110 190 106 172 106 140 L106 82 Z" fill={fill(lungs)} stroke={stroke(lungs)} stroke-width="3" />
		<!-- Heart, on the crew member's left (viewer's right), drawn over the lungs -->
		<path
			d="M118 178 C84 154 90 122 108 126 C114 128 118 134 118 138 C118 134 122 128 128 126 C146 122 152 154 118 178 Z"
			fill={fill(heart)}
			stroke={stroke(heart)}
			stroke-width="3.5"
			paint-order="stroke"
		/>
		{@render glyph(lungs, 66, 128)}
		{@render glyph(heart, 118, 148)}
	</svg>

	<figcaption class="space-y-2 text-base">
		<p class="flex flex-wrap items-center gap-x-2">
			<span class="font-semibold">Heart</span>
			{#if heart}<StatusBadge level={heart} />{:else}<span class="text-sm text-muted">no status yet</span>{/if}
		</p>
		<p class="flex flex-wrap items-center gap-x-2">
			<span class="font-semibold">Lungs</span>
			{#if lungs}<StatusBadge level={lungs} />{:else}<span class="text-sm text-muted">no status yet</span>{/if}
		</p>
		<p class="max-w-56 text-xs text-muted">Heart: worst of resting HR and BP. Lungs: SpO2. Reading status only, not a diagnosis.</p>
	</figcaption>
</figure>
