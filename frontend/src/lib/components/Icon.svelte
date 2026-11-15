<script lang="ts">
	type Name = 'circle-check' | 'triangle' | 'octagon' | 'clock' | 'plus' | 'minus';
	let { name, class: className = 'size-5' }: { name: Name; class?: string } = $props();
</script>

<svg
	viewBox="0 0 24 24"
	fill="none"
	stroke="currentColor"
	stroke-width="2"
	stroke-linecap="round"
	stroke-linejoin="round"
	class={className}
	aria-hidden="true"
>
	{#if name === 'circle-check'}
		<circle cx="12" cy="12" r="9" /><path d="m8 12 3 3 5-6" />
	{:else if name === 'triangle'}
		<path d="M12 3 22 20H2L12 3Z" /><path d="M12 10v4M12 17v.01" />
	{:else if name === 'octagon'}
		<path d="M8 2h8l6 6v8l-6 6H8l-6-6V8l6-6Z" /><path d="M12 7v6M12 16.5v.01" />
	{:else if name === 'clock'}
		<circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" />
	{:else if name === 'plus'}
		<path d="M12 5v14M5 12h14" />
	{:else}
		<path d="M5 12h14" />
	{/if}
</svg>
