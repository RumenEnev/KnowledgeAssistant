import { Component, HostBinding, Input } from '@angular/core';
import { marked } from 'marked';
import markedKatex from 'marked-katex-extension';

marked.use(markedKatex({ throwOnError: false, output: 'html' }));

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
    const text = this.msg?.text ?? '';
    // marked.parse is synchronous for the default (non-async) options used here.
    return marked.parse(text, { async: false }) as string;
  }

  copyMessage(text: string) {
    navigator.clipboard.writeText(text);
  }
}
