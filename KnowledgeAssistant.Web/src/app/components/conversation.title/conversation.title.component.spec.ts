import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConversationTitleComponent } from './conversation.title.component';

describe('ConversationTitleComponent', () => {
  let component: ConversationTitleComponent;
  let fixture: ComponentFixture<ConversationTitleComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConversationTitleComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(ConversationTitleComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
