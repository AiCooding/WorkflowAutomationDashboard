import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SignalRService } from './signalr.service';

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);

  private permissionRequested = false;

  constructor() {
    this.signalR.approvalRequested$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((approval) => {
        const label = approval.stepName ? `${approval.stepName} is waiting for approval.` : 'A pipeline run is waiting for your approval.';
        this.notify('Approval requested', label);
      });
  }

  async ensurePermission(): Promise<NotificationPermission> {
    if (!('Notification' in window)) {
      return 'denied';
    }
    if (Notification.permission === 'granted') return 'granted';
    if (Notification.permission === 'denied') return 'denied';
    if (this.permissionRequested) return Notification.permission;
    this.permissionRequested = true;
    try {
      return await Notification.requestPermission();
    } catch {
      return Notification.permission;
    }
  }

  private notify(title: string, body: string): void {
    if (!('Notification' in window)) return;
    if (Notification.permission !== 'granted') {
      void this.ensurePermission().then((perm) => {
        if (perm === 'granted') {
          this.fire(title, body);
        }
      });
      return;
    }
    this.fire(title, body);
  }

  private fire(title: string, body: string): void {
    try {
      new Notification(title, { body, icon: 'favicon.ico' });
    } catch (err) {
      console.warn('Notification failed:', err);
    }
  }
}
