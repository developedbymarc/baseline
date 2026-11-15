<script lang="ts">
	import type { Level } from '#lib/api/types.ts';
	import { LEVEL_WORD } from '#lib/format.ts';
	import Icon from './Icon.svelte';

	type Props = {
		level: Level;
		/** sm: inline word + icon. lg: the large block on the status screen. */
		size?: 'sm' | 'lg';
		/** Second line in the lg block, e.g. "Lowered from WATCH by short sleep". */
		note?: string;
		/** Append * to the word (ground view: level was lowered by context). */
		asterisk?: boolean;
	};
	let { level, size = 'sm', note, asterisk = false }: Props = $props();

	// Shape + word + colour: colour is never the only signal.
	const tone = {
		Green: { icon: 'circle-check', text: 'text-ok', box: 'border-ok bg-ok-surface' },
		Yellow: { icon: 'triangle', text: 'text-watch', box: 'border-watch bg-watch-surface' },
		Red: { icon: 'octagon', text: 'text-act-ink', box: 'border-act bg-act-surface' }
	} as const;
	const t = $derived(tone[level]);
</script>

{#if size === 'lg'}
	<div class="flex items-center gap-4 rounded-2xl border-2 p-4 {t.box} {t.text}">
		<Icon name={t.icon} class="size-12 shrink-0" />
		<div>
			<p class="font-display text-4xl leading-none font-bold">{LEVEL_WORD[level]}</p>
			{#if note}<p class="mt-2 text-base text-ink">{note}</p>{/if}
		</div>
	</div>
{:else}
	<span class="inline-flex items-center gap-1.5 text-sm font-semibold {t.text}">
		<Icon name={t.icon} class="size-4" />{LEVEL_WORD[level]}{asterisk ? '*' : ''}
	</span>
{/if}
