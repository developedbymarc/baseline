<script lang="ts">
	import { page } from '$app/state';
</script>

<main class="grid min-h-dvh place-items-center p-6 text-center">
	<div class="space-y-4">
		<h1 class="font-display text-4xl font-bold">{page.status}</h1>
		<p class="text-muted">{page.error?.message ?? 'Something went wrong.'}</p>
		<a href="/" class="inline-block font-semibold text-action underline underline-offset-4">Back to start</a>
	</div>
</main>
