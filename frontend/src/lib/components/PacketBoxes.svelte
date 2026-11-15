<script lang="ts">
	import type { Packet } from '#lib/api/types.ts';

	let { packet }: { packet: Packet } = $props();
	const d = $derived(packet.data);

	const boxes = $derived([
		{ key: 'S', title: 'Situation', tone: 'text-act-ink' },
		{ key: 'B', title: 'Background', tone: 'text-action' },
		{ key: 'A', title: 'Assessment', tone: 'text-watch' },
		{ key: 'R', title: 'Recommendation', tone: 'text-ok' }
	]);
</script>

<div class="grid gap-4 md:grid-cols-2">
	{#each boxes as box (box.key)}
		<section class="min-h-44 rounded-2xl border border-line bg-surface p-5">
			<h3 class="text-sm font-semibold {box.tone}">{box.title}</h3>
			<div class="mt-3 space-y-2 text-base leading-relaxed">
				{#if box.key === 'S'}
					<p>{d.trigger.summary}</p>
					{#each d.trigger.symptoms as s (s.loggedMet + s.symptomCode)}
						<p>
							Symptom: {s.symptomCode.replaceAll('_', ' ')}{s.note ? `, “${s.note}”` : ''} ({s.loggedMet})
						</p>
					{/each}
				{:else if box.key === 'B'}
					<p>
						{d.crew.role}, {d.crew.age} y, {d.crew.sex}. Mission day {d.mission.missionDay}{d.mission
							.inAdaptationWindow
							? ' (adaptation window)'
							: ''}.
					</p>
					{#each d.baselines as b (b.summary)}<p>{b.summary}</p>{/each}
				{:else if box.key === 'A'}
					{#if d.contextFactors.length}
						<p>Possible factors (not a diagnosis):</p>
						<ul class="list-disc pl-5">
							{#each d.contextFactors as f (f.ruleId)}<li>{f.message}</li>{/each}
						</ul>
					{:else}
						<p>No known factor found. Not a diagnosis.</p>
					{/if}
					{#if d.protocol}
						<p class="text-muted">
							Protocol {d.protocol.title} v{d.protocol.version}: {d.protocol.steps.length} steps logged.
						</p>
					{/if}
				{:else}
					<p>{d.recommendation}</p>
				{/if}
			</div>
		</section>
	{/each}
</div>
