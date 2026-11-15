<script lang="ts" module>
	import * as echarts from 'echarts/core';
	import { LineChart } from 'echarts/charts';
	import {
		AriaComponent, GridComponent, MarkAreaComponent, TooltipComponent
	} from 'echarts/components';
	import { CanvasRenderer } from 'echarts/renderers';

	echarts.use([LineChart, GridComponent, MarkAreaComponent, TooltipComponent, AriaComponent, CanvasRenderer]);

	export interface TrendPoint {
		label: string;
		value: number;
		level: 'Green' | 'Yellow' | 'Red' | null;
	}
	export interface Band {
		low: number;
		high: number;
		label: string;
	}
</script>

<script lang="ts">
	type Props = { points: TrendPoint[]; unit: string; normal?: Band | null; baseline?: Band | null };
	let { points, unit, normal = null, baseline = null }: Props = $props();

	let el = $state<HTMLDivElement>();

	// Colours come from the Tailwind tokens, so the chart never drifts from the theme.
	const token = (name: string) =>
		getComputedStyle(document.documentElement).getPropertyValue(name).trim();

	// Shape as well as colour: circle = OK, triangle = WATCH, octagon = ACT NOW.
	const OCTAGON = 'path://M3,0 L7,0 L10,3 L10,7 L7,10 L3,10 L0,7 L0,3 Z';
	const shape = { Green: 'circle', Yellow: 'triangle', Red: OCTAGON } as const;
	const colour = { Green: '--color-ok', Yellow: '--color-watch', Red: '--color-act' } as const;

	$effect(() => {
		if (!el) return;
		const chart = echarts.init(el);
		const ink = token('--color-ink');
		const muted = token('--color-muted');

		const band = (b: Band, fill: string, position: 'insideTopLeft' | 'insideBottomLeft') => [
			{ yAxis: b.low, name: b.label, itemStyle: { color: fill }, label: { color: muted, position } },
			{ yAxis: b.high }
		];
		const values = [
			...points.map((p) => p.value),
			...[normal, baseline].flatMap((b) => (b ? [b.low, b.high] : []))
		];
		const pad = (Math.max(...values) - Math.min(...values)) * 0.1 || 1;

		chart.setOption({
			aria: { enabled: true },
			animation: false,
			grid: { left: 56, right: 24, top: 24, bottom: 36 },
			tooltip: { trigger: 'axis', valueFormatter: (v: number) => `${v} ${unit}` },
			xAxis: {
				type: 'category',
				data: points.map((p) => p.label),
				axisLabel: { color: muted },
				axisLine: { lineStyle: { color: token('--color-line') } }
			},
			yAxis: {
				type: 'value',
				min: Math.floor(Math.min(...values) - pad),
				max: Math.ceil(Math.max(...values) + pad),
				axisLabel: { color: muted },
				splitLine: { lineStyle: { color: token('--color-line'), opacity: 0.4 } }
			},
			series: [
				{
					type: 'line',
					data: points.map((p) => ({
						value: p.value,
						symbol: p.level ? shape[p.level] : 'circle',
						symbolSize: p.level === 'Red' ? 16 : 11,
						itemStyle: { color: p.level ? token(colour[p.level]) : ink }
					})),
					lineStyle: { color: ink, width: 2 },
					markArea: {
						silent: true,
						data: [
							...(normal ? [band(normal, 'rgba(143,184,255,0.08)', 'insideTopLeft')] : []),
							...(baseline ? [band(baseline, 'rgba(143,184,255,0.22)', 'insideBottomLeft')] : [])
						]
					}
				}
			]
		});

		const observer = new ResizeObserver(() => chart.resize());
		observer.observe(el);
		return () => {
			observer.disconnect();
			chart.dispose();
		};
	});
</script>

<!-- Height follows the viewport (240-384 px) so the page fits a landscape tablet. -->
<div bind:this={el} class="h-[clamp(240px,calc(100dvh-520px),384px)] w-full"></div>
