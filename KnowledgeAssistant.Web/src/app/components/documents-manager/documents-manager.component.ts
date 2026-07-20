import { Component, EventEmitter, inject, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DocumentsService } from '../../services/documents.service';
import { NotificationService } from '../../services/notification.service';
import { DocumentItem } from '../../models/document';

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

  title = signal('');
  topics = signal('');
  text = signal('');

  async ngOnInit() {
    await this.loadDocuments();
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

  async addDocument() {
    const title = this.title().trim();
    const text = this.text().trim();
    const topics = this.topics()
      .split(',')
      .map(t => t.trim())
      .filter(t => t.length > 0);

    if (!title || !text || topics.length === 0) {
      this.notificationService.error('Title, text and at least one topic are required.');
      return;
    }

    this.isSaving.set(true);
    try {
      await this.documentsService.ingestText(title, text, topics);
      this.title.set('');
      this.topics.set('');
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
