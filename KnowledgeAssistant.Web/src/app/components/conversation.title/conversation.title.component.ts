import { Component, ElementRef, HostListener, Input, Output, EventEmitter, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Topic } from '../../models/document';

interface Item {
  id: string;
  title: string;
  topic?: string;
  topicId?: number;
}

@Component({
  selector: 'app-conversation-title',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './conversation.title.component.html',
  styleUrl: './conversation.title.component.css'
})
export class ConversationTitleComponent {
  @Input() item!: Item;
  @Input() topics: Topic[] = [];
  @Output() rename = new EventEmitter<string>();
  @Output() delete = new EventEmitter<void>();
  @Output() topicChange = new EventEmitter<number | null>();

  @ViewChild('editInput') editInput!: ElementRef<HTMLInputElement>;

  showMenu = false;
  showTopicMenu = false;
  menuTop = 0;
  menuLeft = 0;
  isEditing = false;
  editValue = '';

  toggleMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.showMenu = !this.showMenu;
    this.showTopicMenu = false;
    if (this.showMenu) {
      const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
      this.menuTop = rect.bottom + 4;
      this.menuLeft = rect.right - 120;
    }
  }

  toggleTopicMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.showTopicMenu = !this.showTopicMenu;
  }

  onSetTopic(topicId: number | null, event: MouseEvent): void {
    event.stopPropagation();
    this.topicChange.emit(topicId);
    this.showTopicMenu = false;
    this.showMenu = false;
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.showMenu = false;
    this.showTopicMenu = false;
  }

  onRename(event: MouseEvent): void {
    event.stopPropagation();
    this.showMenu = false;
    this.editValue = this.item.title;
    this.isEditing = true;
    setTimeout(() => {
      this.editInput?.nativeElement.select();
    });
  }

  commitRename(): void {
    const trimmed = this.editValue.trim();
    if (trimmed && trimmed !== this.item.title) {
      this.rename.emit(trimmed);
    }
    this.isEditing = false;
  }

  cancelRename(): void {
    this.isEditing = false;
  }

  onDelete(event: MouseEvent): void {
    event.stopPropagation();
    this.delete.emit();
    this.showMenu = false;
  }
}