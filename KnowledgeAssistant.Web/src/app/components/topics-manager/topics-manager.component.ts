import { Component, EventEmitter, inject, OnInit, Output, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgTemplateOutlet } from '@angular/common';
import { DocumentsService } from '../../services/documents.service';
import { NotificationService } from '../../services/notification.service';
import { Topic } from '../../models/document';

interface TopicNode {
  topic: Topic;
  children: TopicNode[];
  expanded: boolean;
}

interface ParentOption {
  id: number | null;
  label: string;
}

@Component({
  selector: 'app-topics-manager',
  standalone: true,
  imports: [FormsModule, NgTemplateOutlet],
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
  newTopicParentId = signal<number | null>(null);
  isCreating = signal(false);
  editingTopicId = signal<number | null>(null);
  editingName = signal('');
  editingParentId = signal<number | null>(null);
  savingTopicId = signal<number | null>(null);
  deletingTopicId = signal<number | null>(null);
  dragOverId = signal<number | null>(null);

  /** Tracks which node ids are collapsed, so re-fetching topics doesn't reset the tree's expand state. */
  private collapsedIds = new Set<number>();
  /** Bumped whenever expand state changes, to force topicTree() to recompute. */
  private expandVersion = signal(0);
  /** Id of the topic currently being dragged; transient, not rendered, so it isn't a signal. */
  private draggingId: number | null = null;

  topicTree = computed<TopicNode[]>(() => {
    this.expandVersion();
    return this.buildTree(this.topics());
  });

  parentOptionsForCreate = computed<ParentOption[]>(() => this.buildParentOptions(this.topics(), null));
  parentOptionsForEdit = computed<ParentOption[]>(() => this.buildParentOptions(this.topics(), this.editingTopicId()));

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
      const topic = await this.documentsService.createTopic(name, this.newTopicParentId());
      this.topics.update(current => [...current, topic]);
      this.newTopicName.set('');
      this.newTopicParentId.set(null);
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to create the topic.'));
    } finally {
      this.isCreating.set(false);
    }
  }

  startEdit(topic: Topic): void {
    this.editingTopicId.set(topic.id);
    this.editingName.set(topic.name);
    this.editingParentId.set(topic.parentId);
  }

  cancelEdit(): void {
    this.editingTopicId.set(null);
    this.editingName.set('');
    this.editingParentId.set(null);
  }

  async saveEdit(topic: Topic) {
    const name = this.editingName().trim();
    if (!name) {
      this.notificationService.error('Topic name is required.');
      return;
    }

    const parentId = this.editingParentId();
    if (parentId === topic.id) {
      this.notificationService.error('A topic cannot be its own parent.');
      return;
    }

    this.savingTopicId.set(topic.id);
    try {
      const updated = await this.documentsService.updateTopic(topic.id, name, parentId);
      this.topics.update(current => current.map(t => t.id === topic.id ? updated : t));
      this.cancelEdit();
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to update the topic.'));
    } finally {
      this.savingTopicId.set(null);
    }
  }

  async deleteTopic(node: TopicNode) {
    const topic = node.topic;
    const message = node.children.length > 0
      ? `Delete '${topic.name}'? It has ${node.children.length} subtopic(s), which will become top-level topics.`
      : `Delete '${topic.name}'?`;

    if (!confirm(message)) {
      return;
    }

    this.deletingTopicId.set(topic.id);
    try {
      await this.documentsService.deleteTopic(topic.id);
      this.topics.update(current => current.filter(t => t.id !== topic.id));
      if (this.editingTopicId() === topic.id) {
        this.cancelEdit();
      }
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to delete the topic.'));
    } finally {
      this.deletingTopicId.set(null);
    }
  }

  toggleExpand(node: TopicNode): void {
    if (this.collapsedIds.has(node.topic.id)) {
      this.collapsedIds.delete(node.topic.id);
    } else {
      this.collapsedIds.add(node.topic.id);
    }
    this.expandVersion.update(v => v + 1);
  }

  // --- Drag-and-drop reparenting ---

  onDragStart(event: DragEvent, node: TopicNode): void {
    this.draggingId = node.topic.id;
    event.dataTransfer?.setData('text/plain', String(node.topic.id));
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
    }
  }

  onDragOver(event: DragEvent, node: TopicNode): void {
    event.preventDefault();
    event.stopPropagation();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
    this.dragOverId.set(node.topic.id);
  }

  onDragLeave(): void {
    this.dragOverId.set(null);
  }

  onRootDragOver(event: DragEvent): void {
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
  }

  async onDrop(event: DragEvent, targetNode: TopicNode): Promise<void> {
    event.preventDefault();
    event.stopPropagation();
    this.dragOverId.set(null);

    const draggedId = this.draggingId;
    this.draggingId = null;
    if (draggedId == null || draggedId === targetNode.topic.id) {
      return;
    }

    if (this.isDescendant(draggedId, targetNode.topic.id)) {
      this.notificationService.error("Can't move a topic into one of its own subtopics.");
      return;
    }

    const draggedTopic = this.topics().find(t => t.id === draggedId);
    if (!draggedTopic || draggedTopic.parentId === targetNode.topic.id) {
      return;
    }

    await this.reparent(draggedTopic, targetNode.topic.id);
  }

  async onDropToRoot(event: DragEvent): Promise<void> {
    event.preventDefault();
    this.dragOverId.set(null);

    const draggedId = this.draggingId;
    this.draggingId = null;
    if (draggedId == null) {
      return;
    }

    const draggedTopic = this.topics().find(t => t.id === draggedId);
    if (!draggedTopic || draggedTopic.parentId == null) {
      return;
    }

    await this.reparent(draggedTopic, null);
  }

  private async reparent(topic: Topic, parentId: number | null): Promise<void> {
    try {
      const updated = await this.documentsService.updateTopic(topic.id, topic.name, parentId);
      this.topics.update(current => current.map(t => t.id === topic.id ? updated : t));
    } catch (err) {
      this.notificationService.error(this.toMessage(err, 'Failed to move the topic.'));
    }
  }

  private isDescendant(ancestorId: number, nodeId: number): boolean {
    const ancestor = this.findNode(this.topicTree(), ancestorId);
    if (!ancestor) {
      return false;
    }

    const search = (n: TopicNode): boolean => n.children.some(c => c.topic.id === nodeId || search(c));
    return search(ancestor);
  }

  private findNode(nodes: TopicNode[], id: number): TopicNode | null {
    for (const node of nodes) {
      if (node.topic.id === id) {
        return node;
      }
      const found = this.findNode(node.children, id);
      if (found) {
        return found;
      }
    }
    return null;
  }

  // --- Tree / parent-option building ---

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

  /** Flat, indented list for the parent picker. Excludes excludeId and all of its descendants. */
  private buildParentOptions(topics: Topic[], excludeId: number | null): ParentOption[] {
    const options: ParentOption[] = [{ id: null, label: '(No parent — top level)' }];

    const childrenByParent = new Map<number | null, Topic[]>();
    for (const topic of topics) {
      const key = topic.parentId ?? null;
      if (!childrenByParent.has(key)) {
        childrenByParent.set(key, []);
      }
      childrenByParent.get(key)!.push(topic);
    }
    for (const list of childrenByParent.values()) {
      list.sort((a, b) => a.name.localeCompare(b.name));
    }

    const excluded = new Set<number>();
    if (excludeId != null) {
      const collect = (id: number) => {
        excluded.add(id);
        for (const child of childrenByParent.get(id) ?? []) {
          collect(child.id);
        }
      };
      collect(excludeId);
    }

    const walk = (parentId: number | null, depth: number) => {
      for (const topic of childrenByParent.get(parentId) ?? []) {
        if (excluded.has(topic.id)) {
          continue;
        }
        const indent = depth > 0 ? '   '.repeat(depth) + '└ ' : '';
        options.push({ id: topic.id, label: indent + topic.name });
        walk(topic.id, depth + 1);
      }
    };
    walk(null, 0);

    return options;
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