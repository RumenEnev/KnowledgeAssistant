import { Component, EventEmitter, inject, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DocumentsService } from '../../services/documents.service';
import { NotificationService } from '../../services/notification.service';
import { Topic } from '../../models/document';

@Component({
  selector: 'app-topics-manager',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './topics-manager.component.html',
  styleUrl: './topics-manager.component.css'
})
export class TopicsManagerComponent implements OnInit {
  @Output() closed = new EventEmitter<void>();

  private documentsService = inject(DocumentsService);
  private notificationService = inject(NotificationService);

  topics = signal<Topic[]>([]);
  isLoading = signal(false);
  newTopicName = signal('');
  isCreating = signal(false);
  editingTopicId = signal<number | null>(null);
  editingName = signal('');
  savingTopicId = signal<number | null>(null);
  deletingTopicId = signal<number | null>(null);

  private overlayMouseDownOnBackdrop = false;

  async ngOnInit() {
    await this.loadTopics();
  }

  async loadTopics() {
    this.isLoading.set(true);
    try {
      const topics = await this.documentsService.getTopics();
      this.topics.set(topics);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to load topics.'));
    } finally {
      this.isLoading.set(false);
    }
  }

  async createTopic() {
    const name = this.newTopicName().trim();
    if (!name) {
      this.notificationService.error('Topic name is required.');
      return;
    }

    this.isCreating.set(true);
    try {
      const topic = await this.documentsService.createTopic(name);
      this.topics.update(current => [...current, topic].sort((a, b) => a.name.localeCompare(b.name)));
      this.newTopicName.set('');
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to create the topic.'));
    } finally {
      this.isCreating.set(false);
    }
  }

  startEdit(topic: Topic): void {
    this.editingTopicId.set(topic.id);
    this.editingName.set(topic.name);
  }

  cancelEdit(): void {
    this.editingTopicId.set(null);
    this.editingName.set('');
  }

  async saveEdit(topic: Topic) {
    const name = this.editingName().trim();
    if (!name) {
      this.notificationService.error('Topic name is required.');
      return;
    }

    this.savingTopicId.set(topic.id);
    try {
      const updated = await this.documentsService.updateTopic(topic.id, name);
      this.topics.update(current =>
        current.map(t => t.id === topic.id ? updated : t).sort((a, b) => a.name.localeCompare(b.name))
      );
      this.cancelEdit();
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to update the topic.'));
    } finally {
      this.savingTopicId.set(null);
    }
  }

  async deleteTopic(topic: Topic) {
    this.deletingTopicId.set(topic.id);
    try {
      await this.documentsService.deleteTopic(topic.id);
      this.topics.update(current => current.filter(t => t.id !== topic.id));
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to delete the topic.'));
    } finally {
      this.deletingTopicId.set(null);
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
