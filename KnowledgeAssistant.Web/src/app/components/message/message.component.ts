import { Component, HostBinding, Input } from '@angular/core';

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

  copyMessage(text: string) {
    navigator.clipboard.writeText(text);
  }
}
