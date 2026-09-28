import { Injectable, NgZone, inject, signal } from '@angular/core';
import { TextToSpeechHandlers, TextToSpeechService } from './text-to-speech.service';

@Injectable()
export class WebTextToSpeechService extends TextToSpeechService {
  private readonly ngZone = inject(NgZone);
  private readonly synth = typeof window !== 'undefined' ? window.speechSynthesis : undefined;
  private currentUtterance: SpeechSynthesisUtterance | null = null;

  private readonly _isSpeaking = signal(false);
  private readonly _isPaused = signal(false);

  readonly isSupported = typeof window !== 'undefined' && 'speechSynthesis' in window;
  readonly isSpeaking = this._isSpeaking.asReadonly();
  readonly isPaused = this._isPaused.asReadonly();

  speak(text: string, handlers?: TextToSpeechHandlers): void {
    if (!this.synth) {
      handlers?.onError?.('Voice output is not supported in this browser.');
      return;
    }

    const trimmed = text?.trim();
    if (!trimmed) {
      return;
    }

    this.stop();

    const utterance = new SpeechSynthesisUtterance(trimmed);
    utterance.lang = navigator.language || 'en-US';

    utterance.onstart = () => this.ngZone.run(() => {
      this._isSpeaking.set(true);
      this._isPaused.set(false);
    });

    utterance.onend = () => this.ngZone.run(() => {
      this.reset();
      handlers?.onEnd?.();
    });

    utterance.onerror = (event: SpeechSynthesisErrorEvent) => this.ngZone.run(() => {
      this.reset();
      if (event.error === 'canceled' || event.error === 'interrupted') {
        handlers?.onEnd?.();
      } else {
        handlers?.onError?.(`Voice output error: ${event.error}`);
      }
    });

    this.currentUtterance = utterance;
    this.synth.speak(utterance);
  }

  pause(): void {
    if (this.synth && this._isSpeaking() && !this._isPaused()) {
      this.synth.pause();
      this._isPaused.set(true);
    }
  }

  resume(): void {
    if (this.synth && this._isPaused()) {
      this.synth.resume();
      this._isPaused.set(false);
    }
  }

  stop(): void {
    if (this.synth && (this.synth.speaking || this.synth.pending)) {
      this.synth.cancel();
    }
    this.reset();
  }

  private reset(): void {
    this._isSpeaking.set(false);
    this._isPaused.set(false);
    this.currentUtterance = null;
  }
}
