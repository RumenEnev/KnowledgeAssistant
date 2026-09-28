import { Signal } from '@angular/core';

export interface SpeechToTextResult {
  transcript: string;
  isFinal: boolean;
}

export interface SpeechToTextHandlers {
  onResult: (result: SpeechToTextResult) => void;
  onError?: (message: string) => void;
  onEnd?: () => void;
}

export abstract class SpeechToTextService {
  abstract readonly isSupported: boolean;
  abstract readonly isListening: Signal<boolean>;
  abstract readonly interimTranscript: Signal<string>;

  abstract start(handlers: SpeechToTextHandlers): void;
  abstract stop(): void;
}
