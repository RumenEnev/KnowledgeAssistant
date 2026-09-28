import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MessageComponent } from './message.component';
import { TextToSpeechService } from '../../services/speech/text-to-speech.service';
import { WebTextToSpeechService } from '../../services/speech/web-text-to-speech.service';

describe('MessageComponent', () => {
  let component: MessageComponent;
  let fixture: ComponentFixture<MessageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MessageComponent],
      providers: [
        { provide: TextToSpeechService, useClass: WebTextToSpeechService }
      ]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(MessageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
