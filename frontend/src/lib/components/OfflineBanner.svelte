<script lang="ts">
	import { mission } from '#lib/stores/mission.svelte.ts';
	import Icon from './Icon.svelte';
</script>

<!-- Shown while GET /api/mission fails (polled every 2 s); goes away on its own when it answers again. -->
{#if mission.offline}
	<div
		role="status"
		class="flex items-center gap-3 border-b-2 border-watch bg-watch-surface px-6 py-3 text-watch"
	>
		<Icon name="triangle" class="size-6 shrink-0" />
		<p>
			<span class="font-semibold">No connection to the BASELINE server.</span>
			<span class="text-ink"
				>What is on screen stays visible; saving and updates resume when it is back.</span
			>
		</p>
	</div>
{/if}
