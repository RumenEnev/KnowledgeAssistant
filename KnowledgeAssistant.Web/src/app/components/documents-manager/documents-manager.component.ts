import { Component, EventEmitter, inject, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DocumentsService } from '../../services/documents.service';
import { NotificationService } from '../../services/notification.service';
import { DocumentItem, Topic } from '../../models/document';

@Component({
  selector: 'app-documents-manager',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './documents-manager.component.html',
  styleUrl: './documents-manager.component.css'
})
export class DocumentsManagerComponent implements OnInit {
  @Output() closed = new EventEmitter<void>();

  private documentsService = inject(DocumentsService);
  private notificationService = inject(NotificationService);

  documents = signal<DocumentItem[]>([]);
  isLoading = signal(false);
  isSaving = signal(false);

  availableTopics = signal<Topic[]>([]);
  selectedTopicNames = signal<Set<string>>(new Set());

  title = signal('');
  text = signal('');

  async ngOnInit() {
    await Promise.all([this.loadDocuments(), this.loadTopics()]);
  }

  async loadDocuments() {
    this.isLoading.set(true);
    try {
      const documents = await this.documentsService.getDocuments();
      this.documents.set(documents);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to load documents.'));
    } finally {
      this.isLoading.set(false);
    }
  }

  async loadTopics() {
    try {
      const topics = await this.documentsService.getTopics();
      this.availableTopics.set(topics);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to load topics.'));
    }
  }

  isTopicSelected(name: string): boolean {
    return this.selectedTopicNames().has(name);
  }

  toggleTopic(name: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedTopicNames.update(current => {
      const next = new Set(current);
      if (checked) {
        next.add(name);
      } else {
        next.delete(name);
      }
      return next;
    });
  }

  async onTextFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    try {
      const content = await file.text();
      this.text.set(content);
      const nameWithoutExtension = file.name.replace(/\.[^/.]+$/, '');
      this.title.set(nameWithoutExtension);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to read the file.'));
    } finally {
      input.value = '';
    }
  }

  async addDocument() {
    const title = this.title().trim();
    const text = this.text().trim();
    const topics = Array.from(this.selectedTopicNames());

    if (!title || !text || topics.length === 0) {
      this.notificationService.error('Title, text and at least one topic are required.');
      return;
    }

    this.isSaving.set(true);
    try {
      await this.documentsService.ingestText(title, text, topics);
      this.title.set('');
      this.selectedTopicNames.set(new Set());
      this.text.set('');
      await this.loadDocuments();
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to add the document.'));
    } finally {
      this.isSaving.set(false);
    }
  }

  async deleteDocument(doc: DocumentItem) {
    try {
      await this.documentsService.deleteDocument(doc.id);
      this.documents.update(current => current.filter(d => d.id !== doc.id));
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to delete the document.'));
    }
  }

  close(): void {
    this.closed.emit();
  }

  private toMessage(err: unknown, fallback: string): string {
    return err instanceof Error && err.message ? err.message : fallback;
  }
}
