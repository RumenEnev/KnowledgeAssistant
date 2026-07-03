import { Component, ElementRef, HostListener, Input, Output, EventEmitter, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface Item {
  id: string;
  title: string;
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
  @Output() rename = new EventEmitter<string>();
  @Output() delete = new EventEmitter<void>();

  @ViewChild('editInput') editInput!: ElementRef<HTMLInputElement>;

  showMenu = false;
  menuTop = 0;
  menuLeft = 0;
  isEditing = false;
  editValue = '';

  toggleMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.showMenu = !this.showMenu;
    if (this.showMenu) {
      const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
      this.menuTop = rect.bottom + 4;
      this.menuLeft = rect.right - 120;
    }
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.showMenu = false;
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