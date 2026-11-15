const escape = (s: string) =>
	s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const inline = (s: string) => escape(s).replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');

/**
 * Tiny, safe renderer for the how-to files: headings, numbered and bullet lists, bold, paragraphs.
 * HTML in the source is escaped, so the result can be used with {@html}.
 */
export function renderMarkdown(source: string) {
	const out: string[] = [];
	let list: 'ol' | 'ul' | null = null;
	const close = () => list && (out.push(`</${list}>`), (list = null));

	for (const raw of source.split(/\r?\n/)) {
		const line = raw.trim();
		const item = /^(\d+\.|[-*])\s+(.*)$/.exec(line);
		if (item) {
			const kind = /\d/.test(item[1]) ? 'ol' : 'ul';
			if (list !== kind) {
				close();
				out.push(
					kind === 'ol'
						? '<ol class="list-decimal space-y-2 pl-6">'
						: '<ul class="list-disc space-y-2 pl-6">'
				);
				list = kind;
			}
			out.push(`<li>${inline(item[2])}</li>`);
			continue;
		}
		close();
		const heading = /^#{1,6}\s+(.*)$/.exec(line);
		if (heading)
			out.push(`<h3 class="font-display text-xl font-semibold">${inline(heading[1])}</h3>`);
		else if (line) out.push(`<p>${inline(line)}</p>`);
	}
	close();
	return out.join('');
}
