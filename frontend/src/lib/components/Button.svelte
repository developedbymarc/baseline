<script lang="ts">
	import type { Snippet } from 'svelte';
	import type { HTMLButtonAttributes } from 'svelte/elements';

	type Props = HTMLButtonAttributes & {
		variant?: 'primary' | 'secondary' | 'danger' | 'watch';
		/** md: 56 px touch target. lg: 72 px main action. xl: 112 px protocol answer. */
		size?: 'md' | 'lg' | 'xl';
		/** Renders a link that looks like a button. */
		href?: string;
		children: Snippet;
	};
	let {
		variant = 'primary',
		size = 'md',
		href,
		class: className = '',
		children,
		...rest
	}: Props = $props();

	const variants = {
		primary: 'bg-action text-on-action hover:brightness-110 disabled:bg-raised disabled:text-muted',
		secondary: 'border border-line bg-surface text-ink hover:bg-raised disabled:text-muted',
		danger: 'border-2 border-act text-act-ink hover:bg-act-surface',
		watch: 'border border-watch text-watch hover:bg-watch-surface'
	};
	const sizes = {
		md: 'min-h-14 px-6 text-lg',
		lg: 'min-h-18 px-8 text-xl',
		xl: 'min-h-28 px-8 text-2xl'
	};
	const classes = $derived(
		`inline-flex items-center justify-center gap-3 rounded-2xl font-display font-semibold transition active:scale-[0.98] active:brightness-90 disabled:cursor-not-allowed disabled:active:scale-100 ${variants[variant]} ${sizes[size]} ${className}`
	);
</script>

{#if href}
	<a {href} class={classes}>{@render children()}</a>
{:else}
	<button type="button" class={classes} {...rest}>{@render children()}</button>
{/if}
