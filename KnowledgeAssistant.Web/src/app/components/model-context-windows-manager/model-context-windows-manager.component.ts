import { Component, EventEmitter, inject, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatService } from '../../services/chat.service';
import { NotificationService } from '../../services/notification.service';
import { ModelContextWindow } from '../../models/model-context-window';

@Component({
  selector: 'app-model-context-windows-manager',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './model-context-windows-manager.component.html',
  styleUrl: './model-context-windows-manager.component.css'
})
export class ModelContextWindowsManagerComponent implements OnInit {
  @Output() closed = new EventEmitter<void>();

  private chatService = inject(ChatService);
  private notificationService = inject(NotificationService);

  models = signal<ModelContextWindow[]>([]);
  isLoading = signal(false);
  savingModelId = signal<string | null>(null);

  private overlayMouseDownOnBackdrop = false;

  async ngOnInit() {
    await this.loadModels();
  }

  async loadModels() {
    this.isLoading.set(true);
    try {
      const models = await this.chatService.getModelContextWindows();
      this.models.set(models);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to load models.'));
    } finally {
      this.isLoading.set(false);
    }
  }

  updateContextWindowTokens(model: ModelContextWindow, value: number | null): void {
    this.models.update(current =>
      current.map(m => m.id === model.id ? { ...m, contextWindowTokens: value } : m)
    );
  }

  async saveContextWindow(model: ModelContextWindow) {
    if (model.contextWindowTokens != null && model.contextWindowTokens <= 0) {
      this.notificationService.error('Context window tokens must be greater than zero.');
      return;
    }

    this.savingModelId.set(model.id);
    try {
      await this.chatService.updateModelContextWindow(model.id, model.contextWindowTokens);
      this.notificationService.success(`Context window saved for ${model.name}.`);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to save the context window.'));
    } finally {
      this.savingModelId.set(null);
    }
  }

  close(): void {
    this.closed.emit();
  }

  onOverlayMouseDown(event: MouseEvent): void {
    this.overlayMouseDownOnBackdrop = event.target === event.currentTarget;
  }

  onOverlayClick(event: MouseEvent): void {
    if (this.overlayMouseDownOnBackdrop && event.target === event.currentTarget) {
      this.close();
    }
    this.overlayMouseDownOnBackdrop = false;
  }

  private toMessage(err: unknown, fallback: string): string {
    return err instanceof Error && err.message ? err.message : fallback;
  }
}
