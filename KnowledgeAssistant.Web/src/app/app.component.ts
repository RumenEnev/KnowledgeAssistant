import { Component, effect, ElementRef, inject, NgZone, OnInit, signal, ViewChild } from '@angular/core';
import { ChatResponseChunk } from './models/chat-response-chunk';
import { Conversation } from './models/conversation';
import { ChatService } from './services/chat.service';
import { NotificationService } from './services/notification.service';
import { FormsModule } from '@angular/forms';
import { SseEvents } from './shared/events/sse-events';
import { ConversationTitleComponent } from './components/conversation.title/conversation.title.component';
import { MessageComponent } from './components/message/message.component';
import { NotificationToastComponent } from './components/notification-toast/notification-toast.component';


@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule, ConversationTitleComponent, MessageComponent, NotificationToastComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  private chatService = inject(ChatService);
  private notificationService = inject(NotificationService);
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

    effect(() => {
      const model = this.selectedModel();
      if (!model) {
        return;
      }

      this.chatService.updateSelectedModel(model).catch(err => {
        this.notificationService.error(this.toMessage(err, 'Failed to save the selected model.'));
      });
    });
  }

  async ngOnInit() {
    try {
      const models = await this.chatService.getModels();
      this.models.set(models.map(model => model.name));
      if (models.length > 0) {
        this.selectedModel.set(models[0].name);
      }

      const savedModel = await this.chatService.getSelectedModel();
      if (savedModel && this.models().includes(savedModel)) {
        this.selectedModel.set(savedModel);
      }

      const conversations = await this.chatService.getConversations();
      this.conversations.set(conversations);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to load initial data.'));
    }
  }

  async selectConversation(conv: Conversation) {
    try {
      const conversation = await this.chatService.getConversation(conv.id);
      if (conversation.messages != null) {
          this.messages.set(conversation.messages.map(msg => ({ role: msg.role, text: msg.content })));
      }
      this.selectedConversation.set(conversation);

      if (conversation.selectedModel && this.models().includes(conversation.selectedModel)) {
        this.selectedModel.set(conversation.selectedModel);
      }
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to load the conversation.'));
    }
  }

  async renameConversation(conv: Conversation, newTitle: string) {
    try {
      await this.chatService.renameConversation(conv.id, newTitle);
      this.conversations.update(current =>
        current.map(c => c.id === conv.id ? { ...c, title: newTitle } : c)
      );
      if (this.selectedConversation()?.id === conv.id) {
        this.selectedConversation.update(c => c ? { ...c, title: newTitle } : c);
      }
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to rename the conversation.'));
    }
  }

  async deleteConversation(conv: Conversation) {
    this.conversations.update(current => current.filter(c => c.id !== conv.id));
    if (this.selectedConversation()?.id === conv.id) {
      this.selectedConversation.set(null);
    }

    try {
      await this.chatService.deleteConversation(conv.id);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to delete the conversation.'));
    }
  }

  newConversation() {
    // Don't create a conversation on the backend yet — the backend creates the
    // real conversation (with a generated title) once the first message is sent.
    this.selectedConversation.set(null);
    this.messages.set([]);
    this.tokenConsumption.set(null);
  }

  private async attachNewConversation(conversationId: string) {
    try {
      const conversation = await this.chatService.getConversation(conversationId);
      this.conversations.update(current => [conversation, ...current]);
      this.selectedConversation.set(conversation);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to load the new conversation.'));
    }
  }

  async send() {
    const text = this.userPrompt().trim();

    if (!text || this.isStreaming()) {
      return;
    }

    const isNewConversation = !this.selectedConversation();
    const conversationId = this.selectedConversation()?.id;
    let newConversationAttached = false;

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
          conversationId,
          message: text,
          model: this.selectedModel()
        },

        // EVENT CALLBACK — runs outside Angular zone (fetch ReadableStream), so wrap with ngZone.run()
        (event: ChatResponseChunk) => this.ngZone.run(() => {
          switch (event.Type) {

            case SseEvents.Token:
              if (isNewConversation && !newConversationAttached && event.conversationId) {
                newConversationAttached = true;
                this.attachNewConversation(event.conversationId);
              }

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
              this.notificationService.error(event.Message ?? 'An error occurred while generating the response.');
              break;
          }
        })
      );

    } catch (err) {
      this.isStreaming.set(false);
      this.notificationService.error(this.toMessage(err, 'Failed to send the message.'));
    }
  }

  private toMessage(err: unknown, fallback: string): string {
    return err instanceof Error && err.message ? err.message : fallback;
  }
}
