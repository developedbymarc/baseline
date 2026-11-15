<script lang="ts">
	import { flow } from '#lib/stores/flow.svelte.ts';
	import { mission } from '#lib/stores/mission.svelte.ts';
	import { latencyText } from '#lib/format.ts';
	import Icon from './Icon.svelte';
</script>

{#if flow.alert}
	<div
		role="alert"
		class="flex items-center justify-between gap-6 border-b-2 border-act bg-act-surface px-6 py-3"
	>
		<div class="flex items-center gap-4">
			<Icon name="octagon" class="size-9 shrink-0 text-act" />
			<div>
				<p class="font-display text-2xl leading-none font-bold text-act">ACT NOW</p>
				<p class="mt-1 text-base">{flow.alert.reason}</p>
			</div>
		</div>
		{#if mission.state?.onboardFirst}
			<p class="max-w-xs text-right text-sm text-muted">
				Ground is {latencyText(mission.state.oneWayLatencySeconds)} away. Follow on-board steps first.
			</p>
		{/if}
	</div>
{/if}
