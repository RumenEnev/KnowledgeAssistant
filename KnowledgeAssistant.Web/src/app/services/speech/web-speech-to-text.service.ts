import { Injectable, NgZone, inject, signal } from '@angular/core';
import { SpeechToTextHandlers, SpeechToTextService } from './speech-to-text.service';

@Injectable()
export class WebSpeechToTextService extends SpeechToTextService {
  private readonly ngZone = inject(NgZone);
  private recognition: SpeechRecognition | null = null;
  private handlers: SpeechToTextHandlers | null = null;

  private readonly _isListening = signal(false);
  private readonly _interimTranscript = signal('');

  private readonly recognitionCtor =
    typeof window !== 'undefined' ? (window.SpeechRecognition ?? window.webkitSpeechRecognition) : undefined;

  readonly isSupported = !!this.recognitionCtor;
  readonly isListening = this._isListening.asReadonly();
  readonly interimTranscript = this._interimTranscript.asReadonly();

  start(handlers: SpeechToTextHandlers): void {
    if (!this.recognitionCtor) {
      handlers.onError?.('Voice input is not supported in this browser. Try Chrome or Edge.');
      return;
    }
    if (this._isListening()) {
      return;
    }

    this.handlers = handlers;

    const recognition = new this.recognitionCtor();
    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.lang = navigator.language || 'en-US';

    recognition.onstart = () => this.ngZone.run(() => {
      this._isListening.set(true);
      this._interimTranscript.set('');
    });

    recognition.onresult = (event: SpeechRecognitionEvent) => this.ngZone.run(() => {
      let interim = '';
      for (let i = event.resultIndex; i < event.results.length; i++) {
        const result = event.results[i];
        const transcript = result[0]?.transcript ?? '';
        if (result.isFinal) {
          this.handlers?.onResult({ transcript, isFinal: true });
        } else {
          interim += transcript;
        }
      }
      this._interimTranscript.set(interim);
      if (interim) {
        this.handlers?.onResult({ transcript: interim, isFinal: false });
      }
    });

    recognition.onerror = (event: SpeechRecognitionErrorEvent) => this.ngZone.run(() => {
      this.handlers?.onError?.(this.describeError(event.error));
    });

    recognition.onend = () => this.ngZone.run(() => {
      this._isListening.set(false);
      this._interimTranscript.set('');
      const endedHandlers = this.handlers;
      this.recognition = null;
      this.handlers = null;
      endedHandlers?.onEnd?.();
    });

    this.recognition = recognition;
    try {
      recognition.start();
    } catch {
      this._isListening.set(false);
      handlers.onError?.('Failed to start voice input. Please try again.');
    }
  }

  stop(): void {
    this.recognition?.stop();
  }

  private describeError(error: string): string {
    switch (error) {
      case 'not-allowed':
      case 'service-not-allowed':
        return 'Microphone access was denied. Please allow microphone permission to use voice input.';
      case 'audio-capture':
        return 'No microphone was found. Please connect a microphone and try again.';
      case 'no-speech':
        return 'No speech detected. Please try again.';
      case 'network':
        return 'A network error occurred during voice input.';
      case 'aborted':
        return '';
      default:
        return `Voice input error: ${error}`;
    }
  }
}
