import { Component, inject, NgZone, OnInit, signal } from '@angular/core';
import { ChatResponseChunk, ChatService, ModelInfo } from './services/chat.service';
import { FormsModule } from '@angular/forms';
import { SseEvents } from './shared/events/sse-events';


@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  private chatService = inject(ChatService);
  private ngZone = inject(NgZone);

  isStreaming = signal(false);
  models = signal<string[]>([]);
  selectedModel = signal<string>('');
  userPrompt = signal<string>('');
  messages = signal([{ role: 'system', text: ''}]);
  conversations= signal<string[]>([]);

  copyMessage(text: string) {
    navigator.clipboard.writeText(text);
  }

  async ngOnInit() {
      const models = await this.chatService.getModels();
      this.models.set(models.map(model => model.name));
      if (models.length > 0) {
        this.selectedModel.set(models[0].name);
      }

      const conversations = await this.chatService.getConversations();
      this.conversations.set(conversations.map(conversation => conversation.title ?? 'Untitled' ));
  }

  async newConversation() {
     const conversation = await this.chatService.newConversation();
     console.log('New conversation created with ID:', conversation);
     this.conversations.update(current => [...current, conversation]);
    }

  async send() {
    const text = this.userPrompt().trim();

    if (!text || this.isStreaming()) {
      return;
    }

    // 1. Add user message
    this.messages.update(current => [
      ...current,
      { role: 'user', text }
    ]);

    this.userPrompt.set('');

    // 2. Add empty assistant message placeholder (for streaming)
    this.messages.update(current => [
      ...current,
      { role: 'assistant', text: '' }
    ]);

    this.isStreaming.set(true);

    try {
      await this.chatService.streamChat(
        {
          message: text,
          model: this.selectedModel()
        },

        // EVENT CALLBACK — runs outside Angular zone (fetch ReadableStream), so wrap with ngZone.run()
        (event: ChatResponseChunk) => this.ngZone.run(() => {
          switch (event.Type) {

            case SseEvents.Token:
              this.messages.update(current => {
                const updated = [...current];
                const lastIndex = updated.length - 1;
                updated[lastIndex] = {
                  ...updated[lastIndex],
                  text: updated[lastIndex].text + (event.content ?? '')
                };
                return updated;
              });
              break;

            case SseEvents.Done:
              this.isStreaming.set(false);
              break;

            case SseEvents.Error:
              this.isStreaming.set(false);
              console.error('Streaming error:', event);
              break;
          }
        })
      );

    } catch (err) {
      this.isStreaming.set(false);
      console.error('Streaming failed', err);
    }
  }

}
