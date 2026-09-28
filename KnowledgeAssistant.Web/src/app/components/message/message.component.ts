import { Component, HostBinding, inject, Input, OnDestroy, signal } from '@angular/core';
import { marked } from 'marked';
import markedKatex from 'marked-katex-extension';
import { TextToSpeechService } from '../../services/speech/text-to-speech.service';

marked.use(markedKatex({ throwOnError: false, output: 'html' }));

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
export class MessageComponent implements OnDestroy {
  @Input() msg!: { role: string; text: string };

  private textToSpeech = inject(TextToSpeechService);
  speechSupported = this.textToSpeech.isSupported;
  isSpeaking = signal(false);
  isPaused = signal(false);

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

  toggleSpeech(): void {
    if (this.isSpeaking()) {
      if (this.isPaused()) {
        this.textToSpeech.resume();
        this.isPaused.set(false);
      } else {
        this.textToSpeech.pause();
        this.isPaused.set(true);
      }
      return;
    }

    this.textToSpeech.speak(this.msg?.text ?? '', {
      onEnd: () => {
        this.isSpeaking.set(false);
        this.isPaused.set(false);
      },
      onError: () => {
        this.isSpeaking.set(false);
        this.isPaused.set(false);
      }
    });
    this.isSpeaking.set(true);
    this.isPaused.set(false);
  }

  stopSpeech(): void {
    this.textToSpeech.stop();
    this.isSpeaking.set(false);
    this.isPaused.set(false);
  }

  ngOnDestroy(): void {
    if (this.isSpeaking()) {
      this.textToSpeech.stop();
    }
  }
}
