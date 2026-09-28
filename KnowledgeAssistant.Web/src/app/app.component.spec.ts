import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';
import { SpeechToTextService } from './services/speech/speech-to-text.service';
import { WebSpeechToTextService } from './services/speech/web-speech-to-text.service';
import { TextToSpeechService } from './services/speech/text-to-speech.service';
import { WebTextToSpeechService } from './services/speech/web-text-to-speech.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: SpeechToTextService, useClass: WebSpeechToTextService },
        { provide: TextToSpeechService, useClass: WebTextToSpeechService }
      ]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it(`should have the 'KnowledgeAssistant.Web' title`, () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.title).toEqual('KnowledgeAssistant.Web');
  });

  it('should render title', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Hello, KnowledgeAssistant.Web');
  });
});
