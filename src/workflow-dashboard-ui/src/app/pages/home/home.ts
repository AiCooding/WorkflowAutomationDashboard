import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { startWith } from 'rxjs';
import { DashboardService } from '../../core/api/dashboard.service';
import { EventsService } from '../../core/api/events.service';
import { DashboardSummary, WorkflowEvent } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';
import { StatusStyle } from '../../shared/status-style';

interface SummaryCard {
  label: string;
  key: keyof DashboardSummary;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-home',
  imports: [
    CommonModule,
    DatePipe,
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatChipsModule,
    MatListModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class HomePage {
  private readonly dashboard = inject(DashboardService);
  private readonly events = inject(EventsService);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);
  readonly styles = inject(StatusStyle);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly recentEvents = signal<WorkflowEvent[]>([]);

  readonly connected = toSignal(this.signalR.connected$.pipe(startWith(false)), { initialValue: false });

  readonly cards: SummaryCard[] = [
    { label: 'Running workflows', key: 'runningWorkflows', icon: 'play_circle', color: '#2563eb' },
    { label: 'Active agents', key: 'activeAgents', icon: 'smart_toy', color: '#0891b2' },
    { label: 'Pending inputs', key: 'pendingInputRequests', icon: 'help', color: '#d97706' },
    { label: 'Failed', key: 'failedWorkflows', icon: 'error', color: '#dc2626' },
    { label: 'Waiting input', key: 'waitingInputWorkflows', icon: 'pause_circle', color: '#9333ea' },
    { label: 'Completed', key: 'completedWorkflows', icon: 'check_circle', color: '#16a34a' },
    { label: 'Features in progress', key: 'featuresInProgress', icon: 'pending_actions', color: '#2563eb' },
    { label: 'Total features', key: 'totalFeatures', icon: 'inventory_2', color: '#6b7280' },
  ];

  constructor() {
    this.refreshSummary();
    this.refreshEvents();

    // Refresh summary on relevant signals
    this.signalR.workflowUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refreshSummary());
    this.signalR.agentUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refreshSummary());
    this.signalR.inputRequested$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refreshSummary());
    this.signalR.inputAnswered$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refreshSummary());

    // Live-prepend events
    this.signalR.eventLogged$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((evt) => {
      this.recentEvents.update((list) => [evt, ...list].slice(0, 20));
    });
  }

  private refreshSummary(): void {
    this.dashboard.summary().subscribe({
      next: (s) => this.summary.set(s),
      error: (err) => console.error('summary failed', err),
    });
  }

  private refreshEvents(): void {
    this.events.list({ limit: 20 }).subscribe({
      next: (list) => this.recentEvents.set(list),
      error: (err) => console.error('events failed', err),
    });
  }
}
