import { Injectable, signal } from '@angular/core';

export type NotificationType = 'error' | 'success' | 'info';

export interface Notification {
  id: number;
  type: NotificationType;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private nextId = 0;
  private readonly autoDismissMs = 6000;

  notifications = signal<Notification[]>([]);

  error(message: string): void {
    this.show('error', message);
  }

  success(message: string): void {
    this.show('success', message);
  }

  info(message: string): void {
    this.show('info', message);
  }

  dismiss(id: number): void {
    this.notifications.update(current => current.filter(n => n.id !== id));
  }

  private show(type: NotificationType, message: string): void {
    const id = this.nextId++;
    this.notifications.update(current => [...current, { id, type, message }]);
    setTimeout(() => this.dismiss(id), this.autoDismissMs);
  }
}
