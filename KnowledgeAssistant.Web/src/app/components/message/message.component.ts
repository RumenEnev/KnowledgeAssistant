import { Component, HostBinding, Input } from '@angular/core';
import { marked } from 'marked';
import markedKatex from 'marked-katex-extension';

marked.use(markedKatex({ throwOnError: false, output: 'html' }));

/**
 * Many LLMs (including Ollama models) emit LaTeX math using the
 * "\( ... \)" (inline) and "\[ ... \]" (block) delimiter conventions
 * instead of the "$...$" / "$$...$$" delimiters that marked-katex-extension
 * recognizes by default. This rewrites the former into the latter so KaTeX
 * can find and render the formulas. Content inside fenced/inline code blocks
 * is left untouched.
 */
function normalizeMathDelimiters(markdown: string): string {
  if (!markdown) {
    return markdown ?? '';
  }

  const codeFenceSplitRegex = /(```[\s\S]*?```|`[^`\n]+`)/g;
  const blockDelimiterRegex = /\\\[([\s\S]+?)\\\]/g;
  const inlineDelimiterRegex = /\\\(([\s\S]+?)\\\)/g;

  return markdown
    .split(codeFenceSplitRegex)
    .map((segment, index) => {
      // Odd indices are the code fence/inline code matches captured by the split regex.
      if (index % 2 === 1) {
        return segment;
      }

      return segment
        .replace(blockDelimiterRegex, (_match, content) => `\n$$\n${content.trim()}\n$$\n`)
        .replace(inlineDelimiterRegex, (_match, content) => `$${content.trim()}$`);
    })
    .join('');
}

@Component({
  selector: 'app-message',
  standalone: true,
  imports: [],
  templateUrl: './message.component.html',
  styleUrls: ['./message.component.css']
})
export class MessageComponent {
  @Input() msg!: { role: string; text: string };

  @HostBinding('class.user') get isUser() { return this.msg?.role === 'user'; }
  @HostBinding('class.assistant') get isAssistant() { return this.msg?.role === 'assistant'; }

  get renderedHtml(): string {
    const text = normalizeMathDelimiters(this.msg?.text ?? '');
    // marked.parse is synchronous for the default (non-async) options used here.
    return marked.parse(text, { async: false }) as string;
  }

  copyMessage(text: string) {
    navigator.clipboard.writeText(text);
  }
}
