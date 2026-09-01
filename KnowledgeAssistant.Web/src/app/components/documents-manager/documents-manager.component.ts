import { Component, ElementRef, EventEmitter, HostListener, OnInit, Output, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgTemplateOutlet } from '@angular/common';
import { DocumentsService } from '../../services/documents.service';
import { NotificationService } from '../../services/notification.service';
import { DocumentItem, Topic, DocumentRetrievalConfig, DEFAULT_RETRIEVAL_CONFIG } from '../../models/document';

interface TopicNode {
  topic: Topic;
  children: TopicNode[];
  expanded: boolean;
}

@Component({
  selector: 'app-documents-manager',
  standalone: true,
  imports: [FormsModule, NgTemplateOutlet],
  templateUrl: './documents-manager.component.html',
  styleUrl: './documents-manager.component.css'
})
export class DocumentsManagerComponent implements OnInit {
  @Output() closed = new EventEmitter<void>();
  @ViewChild('topicsDropdownWrapper') private topicsDropdownWrapper?: ElementRef<HTMLElement>;

  private documentsService = inject(DocumentsService);
  private notificationService = inject(NotificationService);

  documents = signal<DocumentItem[]>([]);
  isLoading = signal(false);
  isSaving = signal(false);

  availableTopics = signal<Topic[]>([]);
  selectedTopicNames = signal<Set<string>>(new Set());
  isTopicsDropdownOpen = signal(false);

  title = signal('');
  text = signal('');

  editingDocumentId = signal<number | null>(null);

  // Per-document retrieval settings (chunking + retrieval tuning), replacing the old global chunking settings.
  retrievalConfig = signal<DocumentRetrievalConfig | null>(null);
  isSavingRetrievalConfig = signal(false);

  /** Panel is visible once there's a document (selected or freshly loaded from file) to configure. */
  isRetrievalPanelVisible = computed(() => this.editingDocumentId() !== null || this.text().trim().length > 0);
  /** Save/Reset are only meaningful once the document actually exists (has a real id). */
  canSaveRetrievalConfig = computed(() => !this.isSavingRetrievalConfig() && this.editingDocumentId() !== null);

  /** Tracks which node ids are collapsed, so re-fetching topics doesn't reset the tree's expand state. */
  private collapsedIds = new Set<number>();
  /** Bumped whenever expand state changes, to force topicTree() to recompute. */
  private expandVersion = signal(0);

  topicTree = computed<TopicNode[]>(() => {
    this.expandVersion();
    return this.buildTree(this.availableTopics());
  });

  selectedTopicsSummary = computed<string>(() => {
    const names = Array.from(this.selectedTopicNames());
    return names.length === 0 ? 'Select topics...' : names.join(', ');
  });

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

  async loadRetrievalConfig(documentId: number) {
    try {
      const config = await this.documentsService.getRetrievalConfig(documentId);
      this.retrievalConfig.set(config);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to load retrieval settings.'));
    }
  }

  updateRetrievalField<K extends keyof DocumentRetrievalConfig>(key: K, value: DocumentRetrievalConfig[K]): void {
    const current = this.retrievalConfig();
    if (!current) {
      return;
    }
    this.retrievalConfig.set({ ...current, [key]: value });
  }

  async saveRetrievalConfig() {
    const config = this.retrievalConfig();
    if (!config) {
      return;
    }

    if (config.chunkOverlap < 0 || config.chunkOverlap >= config.chunkSize) {
      this.notificationService.error('Chunk overlap must be zero or greater, and smaller than chunk size.');
      return;
    }

    this.isSavingRetrievalConfig.set(true);
    try {
      await this.documentsService.saveRetrievalConfig(config);
      this.notificationService.success('Retrieval settings saved.');
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to save retrieval settings.'));
    } finally {
      this.isSavingRetrievalConfig.set(false);
    }
  }

  async resetRetrievalConfig() {
    const documentId = this.editingDocumentId();
    if (documentId === null) {
      return;
    }

    try {
      await this.documentsService.resetRetrievalConfig(documentId);
      await this.loadRetrievalConfig(documentId);
      this.notificationService.success('Retrieval settings reset to default.');
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to reset retrieval settings.'));
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

  toggleTopicsDropdown(): void {
    if (this.availableTopics().length === 0) {
      return;
    }
    this.isTopicsDropdownOpen.update(v => !v);
  }

  closeTopicsDropdown(): void {
    this.isTopicsDropdownOpen.set(false);
  }

  toggleExpand(node: TopicNode): void {
    if (this.collapsedIds.has(node.topic.id)) {
      this.collapsedIds.delete(node.topic.id);
    } else {
      this.collapsedIds.add(node.topic.id);
    }
    this.expandVersion.update(v => v + 1);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.isTopicsDropdownOpen()) {
      return;
    }
    const target = event.target as Node;
    if (this.topicsDropdownWrapper && !this.topicsDropdownWrapper.nativeElement.contains(target)) {
      this.closeTopicsDropdown();
    }
  }

  private buildTree(topics: Topic[]): TopicNode[] {
    const byId = new Map<number, TopicNode>();
    for (const topic of topics) {
      byId.set(topic.id, { topic, children: [], expanded: !this.collapsedIds.has(topic.id) });
    }

    const roots: TopicNode[] = [];
    for (const node of byId.values()) {
      const parentId = node.topic.parentId;
      if (parentId != null && byId.has(parentId)) {
        byId.get(parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    }

    const sortRec = (nodes: TopicNode[]) => {
      nodes.sort((a, b) => a.topic.name.localeCompare(b.topic.name));
      nodes.forEach(n => sortRec(n.children));
    };
    sortRec(roots);

    return roots;
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
      // No real document yet - show defaults so the panel has something to display;
      // Save/Reset stay disabled until the document is actually created (see canSaveRetrievalConfig).
      this.retrievalConfig.set({ documentId: 0, ...DEFAULT_RETRIEVAL_CONFIG });
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
    this.retrievalConfig.set(null);
    this.loadRetrievalConfig(doc.id);
  }

  cancelEdit(): void {
    this.editingDocumentId.set(null);
    this.title.set('');
    this.text.set('');
    this.selectedTopicNames.set(new Set());
    this.retrievalConfig.set(null);
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