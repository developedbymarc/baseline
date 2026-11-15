<script lang="ts">
	type Name =
		| 'circle-check'
		| 'triangle'
		| 'octagon'
		| 'clock'
		| 'plus'
		| 'minus'
		| 'heart'
		| 'gauge'
		| 'thermometer'
		| 'droplet'
		| 'moon'
		| 'smile'
		| 'activity';
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
	{:else if name === 'minus'}
		<path d="M5 12h14" />
	{:else if name === 'heart'}
		<path
			d="M19 14c1.5-1.5 3-3.2 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.8 0-3 .5-4.5 2-1.5-1.5-2.7-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4 3 5.5l7 7 7-7Z"
		/>
	{:else if name === 'gauge'}
		<path d="m12 14 4-4" /><path d="M3.3 19a10 10 0 1 1 17.4 0" />
	{:else if name === 'thermometer'}
		<path d="M14 4v10.5a4 4 0 1 1-4 0V4a2 2 0 0 1 4 0Z" />
	{:else if name === 'droplet'}
		<path
			d="M12 22a7 7 0 0 0 7-7c0-2-1-3.9-3-5.5S12.5 5.5 12 3c-.5 2.5-2 4.9-4 6.5S5 13 5 15a7 7 0 0 0 7 7Z"
		/>
	{:else if name === 'moon'}
		<path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z" />
	{:else if name === 'smile'}
		<circle cx="12" cy="12" r="9" /><path d="M8 14s1.5 2 4 2 4-2 4-2M9 9h.01M15 9h.01" />
	{:else}
		<path d="M22 12h-4l-3 9L9 3l-3 9H2" />
	{/if}
</svg>
