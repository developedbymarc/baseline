<script lang="ts">
	import Button from './Button.svelte';

	/** An error with a plain message and, when the action can be repeated, a retry button. */
	let { message, onretry }: { message: string; onretry?: () => void } = $props();
</script>

<div
	role="alert"
	class="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-act bg-act-surface p-4 text-act-ink"
>
	<p>{message}</p>
	{#if onretry}<Button variant="secondary" onclick={onretry}>Try again</Button>{/if}
</div>
