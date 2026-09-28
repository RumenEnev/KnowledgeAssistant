import { Signal } from '@angular/core';

export interface TextToSpeechHandlers {
  onEnd?: () => void;
  onError?: (message: string) => void;
}

export abstract class TextToSpeechService {
  abstract readonly isSupported: boolean;
  abstract readonly isSpeaking: Signal<boolean>;
  abstract readonly isPaused: Signal<boolean>;

  abstract speak(text: string, handlers?: TextToSpeechHandlers): void;
  abstract pause(): void;
  abstract resume(): void;
  abstract stop(): void;
}
