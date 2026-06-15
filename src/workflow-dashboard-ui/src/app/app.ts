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
  readonly helpUrl = 'https://aicooding.github.io/WorkflowAutomationDashboard/';

  readonly pendingApprovals = signal(0);

  readonly navItems: NavItem[] = [
    { label: 'Dashboard', route: '/', icon: 'dashboard', exact: true },
    { label: 'Control', route: '/control', icon: 'settings_remote' },
    { label: 'Pipelines', route: '/pipelines', icon: 'account_tree' },
    { label: 'Repositories', route: '/repositories', icon: 'folder' },
    { label: 'Agents', route: '/agents', icon: 'smart_toy' },
    { label: 'Features', route: '/features', icon: 'inventory_2' },
    { label: 'Settings', route: '/settings', icon: 'settings' },
  ];

  constructor() {
    this.refreshPendingApprovals();

    this.signalR.approvalRequested$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.pendingApprovals.update((n) => n + 1));

    this.signalR.approvalDecided$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshPendingApprovals());

    // When a run is cancelled or fails any pending approvals are dismissed
    // server-side, so the badge must be re-synced from the source of truth.
    this.signalR.pipelineRunUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((run) => {
        if (run.status === 'cancelled' || run.status === 'failed') {
          this.refreshPendingApprovals();
        }
      });

    void this.notifications.ensurePermission();
  }

  private refreshPendingApprovals(): void {
    this.dashboard
      .summary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (s) => this.pendingApprovals.set(s.pendingApprovals),
        error: () => this.pendingApprovals.set(0),
      });
  }
}
