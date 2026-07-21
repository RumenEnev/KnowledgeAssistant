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

  editingDocumentId = signal<number | null>(null);

  private overlayMouseDownOnBackdrop = false;

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

  selectDocument(doc: DocumentItem): void {
    this.editingDocumentId.set(doc.id);
    this.title.set(doc.title);
    this.text.set(doc.originalText);
    this.selectedTopicNames.set(new Set(doc.topics));
  }

  cancelEdit(): void {
    this.editingDocumentId.set(null);
    this.title.set('');
    this.text.set('');
    this.selectedTopicNames.set(new Set());
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
      const editingId = this.editingDocumentId();
      if (editingId !== null) {
        await this.documentsService.updateDocument(editingId, title, text, topics);
      } else {
        await this.documentsService.ingestText(title, text, topics);
      }
      this.cancelEdit();
      await this.loadDocuments();
    } catch (err) {
      this.notificationService.error(this.toMessage(err, this.editingDocumentId() !== null ? 'Failed to update the document.' : 'Failed to add the document.'));
    } finally {
      this.isSaving.set(false);
    }
  }

  async deleteDocument(doc: DocumentItem) {
    try {
      await this.documentsService.deleteDocument(doc.id);
      this.documents.update(current => current.filter(d => d.id !== doc.id));
      if (this.editingDocumentId() === doc.id) {
        this.cancelEdit();
      }
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to delete the document.'));
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
