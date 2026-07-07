import { Component, effect, ElementRef, inject, NgZone, OnInit, signal, ViewChild } from '@angular/core';
import { ChatResponseChunk } from './models/chat-response-chunk';
import { Conversation } from './models/conversation';
import { ChatService } from './services/chat.service';
import { FormsModule } from '@angular/forms';
import { SseEvents } from './shared/events/sse-events';
import { ConversationTitleComponent } from './components/conversation.title/conversation.title.component';
import { MessageComponent } from './components/message/message.component';


@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule, ConversationTitleComponent, MessageComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  private chatService = inject(ChatService);
  private ngZone = inject(NgZone);

  @ViewChild('messageList') private messageListRef!: ElementRef<HTMLElement>;

  isStreaming = signal(false);
  models = signal<string[]>([]);
  selectedModel = signal<string>('');
  userPrompt = signal<string>('');
  messages = signal([{ role: 'user', text: ''}]);
  conversations = signal<Conversation[]>([]);
  selectedConversation = signal<Conversation | null>(null);
  tokenConsumption = signal<{ prompt: number; response: number; total: number } | null>(null);

  constructor() {
    effect(() => {
      this.messages();
      setTimeout(() => {
        if (this.messageListRef) {
          const el = this.messageListRef.nativeElement;
          el.scrollTop = el.scrollHeight;
        }
      }, 0);
    });
  }

  async ngOnInit() {
      const models = await this.chatService.getModels();
      this.models.set(models.map(model => model.name));
      if (models.length > 0) {
        this.selectedModel.set(models[0].name);
      }

      const conversations = await this.chatService.getConversations();
      this.conversations.set(conversations);
  }

  async selectConversation(conv: Conversation) {
    const conversation = await this.chatService.getConversation(conv.id);
    if (conversation.messages != null) {
        this.messages.set(conversation.messages.map(msg => ({ role: msg.role, text: msg.content })));
    }
    this.selectedConversation.set(conversation);
  }

  async renameConversation(conv: Conversation, newTitle: string) {
    await this.chatService.renameConversation(conv.id, newTitle);
    this.conversations.update(current =>
      current.map(c => c.id === conv.id ? { ...c, title: newTitle } : c)
    );
    if (this.selectedConversation()?.id === conv.id) {
      this.selectedConversation.update(c => c ? { ...c, title: newTitle } : c);
    }
  }

  deleteConversation(conv: Conversation) {
    this.conversations.update(current => current.filter(c => c.id !== conv.id));
    if (this.selectedConversation()?.id === conv.id) {
      this.chatService.deleteConversation(conv.id);
      this.selectedConversation.set(null);
    }
  }

  async newConversation() {
    const conversation = await this.chatService.newConversation();
    this.conversations.update(current => [conversation, ...current]);
    await this.selectConversation(conversation);
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
              if (event.PromptTokens != null && event.ResponseTokens != null) {
                this.tokenConsumption.set({
                  prompt: event.PromptTokens,
                  response: event.ResponseTokens,
                  total: event.PromptTokens + event.ResponseTokens
                });
                setTimeout(() => this.ngZone.run(() => this.tokenConsumption.set(null)), 2500);
              }
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
