import { Component, EventEmitter, HostListener, Output, signal } from '@angular/core';

@Component({
  selector: 'app-main-menu',
  standalone: true,
  imports: [],
  templateUrl: './main-menu.component.html',
  styleUrl: './main-menu.component.css'
})
export class MainMenuComponent {
  @Output() newConversation = new EventEmitter<void>();
  @Output() manageDocuments = new EventEmitter<void>();
  @Output() manageModelContextWindows = new EventEmitter<void>();
  @Output() manageTopics = new EventEmitter<void>();
  @Output() exit = new EventEmitter<void>();

  isOpen = signal(false);

  toggleMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.isOpen.update(open => !open);
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.isOpen.set(false);
  }

  onNewConversation(event: MouseEvent): void {
    event.stopPropagation();
    this.isOpen.set(false);
    this.newConversation.emit();
  }

  onManageDocuments(event: MouseEvent): void {
    event.stopPropagation();
    this.isOpen.set(false);
    this.manageDocuments.emit();
  }

  onManageModelContextWindows(event: MouseEvent): void {
    event.stopPropagation();
    this.isOpen.set(false);
    this.manageModelContextWindows.emit();
  }

  onManageTopics(event: MouseEvent): void {
    event.stopPropagation();
    this.isOpen.set(false);
    this.manageTopics.emit();
  }

  onExit(event: MouseEvent): void {
    event.stopPropagation();
    this.isOpen.set(false);
    this.exit.emit();
  }
}
