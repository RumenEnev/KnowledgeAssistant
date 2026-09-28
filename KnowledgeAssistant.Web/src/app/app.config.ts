import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { SpeechToTextService } from './services/speech/speech-to-text.service';
import { WebSpeechToTextService } from './services/speech/web-speech-to-text.service';
import { TextToSpeechService } from './services/speech/text-to-speech.service';
import { WebTextToSpeechService } from './services/speech/web-text-to-speech.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    { provide: SpeechToTextService, useClass: WebSpeechToTextService },
    { provide: TextToSpeechService, useClass: WebTextToSpeechService }
  ]
};
