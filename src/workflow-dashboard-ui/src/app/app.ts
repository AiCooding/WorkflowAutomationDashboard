import { AsyncPipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { DashboardService } from './core/api/dashboard.service';
import { NotificationsService } from './core/realtime/notifications.service';
import { SignalRService } from './core/realtime/signalr.service';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  exact?: boolean;
}

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AsyncPipe,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatBadgeModule,
    MatTooltipModule,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly dashboard = inject(DashboardService);
  private readonly signalR = inject(SignalRService);
  private readonly notifications = inject(NotificationsService);
  private readonly destroyRef = inject(DestroyRef);

  readonly title = 'Workflow Dashboard';
  readonly connected$ = this.signalR.connected$;

  readonly pendingInputs = signal(0);

  readonly navItems: NavItem[] = [
    { label: 'Dashboard', route: '/', icon: 'dashboard', exact: true },
    { label: 'Features', route: '/features', icon: 'inventory_2' },
    { label: 'Workflows', route: '/workflows', icon: 'account_tree' },
    { label: 'Agents', route: '/agents', icon: 'smart_toy' },
    { label: 'Inputs', route: '/inputs', icon: 'help' },
    { label: 'Control', route: '/control', icon: 'settings_remote' },
    { label: 'Events', route: '/events', icon: 'list_alt' },
  ];

  constructor() {
    this.refreshPendingInputs();

    this.signalR.inputRequested$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.pendingInputs.update((n) => n + 1));

    this.signalR.inputAnswered$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshPendingInputs());

    // ask for notification permission proactively (best-effort)
    void this.notifications.ensurePermission();
  }

  private refreshPendingInputs(): void {
    this.dashboard
      .summary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (s) => this.pendingInputs.set(s.pendingInputRequests),
        error: () => this.pendingInputs.set(0),
      });
  }
}
