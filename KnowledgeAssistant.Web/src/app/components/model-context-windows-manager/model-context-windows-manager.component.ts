import { Component, EventEmitter, inject, OnInit, Output, signal } from '@angular/core';
import { ChatService } from '../../services/chat.service';
import { NotificationService } from '../../services/notification.service';
import { ModelContextWindow } from '../../models/model-context-window';

@Component({
  selector: 'app-model-context-windows-manager',
  standalone: true,
  imports: [],
  templateUrl: './model-context-windows-manager.component.html',
  styleUrl: './model-context-windows-manager.component.css'
})
export class ModelContextWindowsManagerComponent implements OnInit {
  @Output() closed = new EventEmitter<void>();

  private chatService = inject(ChatService);
  private notificationService = inject(NotificationService);

  models = signal<ModelContextWindow[]>([]);
  isLoading = signal(false);

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

  formatSize(bytes: number): string {
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = bytes;
    let unitIndex = 0;
    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }
    return `${Math.round(size * 100) / 100} ${units[unitIndex]}`;
  }
}
