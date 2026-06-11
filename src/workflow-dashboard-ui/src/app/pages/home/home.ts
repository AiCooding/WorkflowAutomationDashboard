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
import { PipelineRunsService } from '../../core/api/pipeline-runs.service';
import { DashboardSummary, PipelineRun } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';
import { StatusStyle } from '../../shared/status-style';

interface SummaryCard {
  label: string;
  key: keyof DashboardSummary;
  icon: string;
  color: string;
  route?: string;
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
  private readonly pipelineRuns = inject(PipelineRunsService);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);
  readonly styles = inject(StatusStyle);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly recentRuns = signal<PipelineRun[]>([]);

  readonly connected = toSignal(this.signalR.connected$.pipe(startWith(false)), { initialValue: false });

  readonly cards: SummaryCard[] = [
    { label: 'Repositories', key: 'totalRepositories', icon: 'folder', color: '#0d9488', route: '/repositories' },
    { label: 'Broken repositories', key: 'brokenRepositories', icon: 'broken_image', color: '#dc2626', route: '/repositories' },
    { label: 'Pipelines', key: 'totalPipelines', icon: 'account_tree', color: '#7c3aed', route: '/pipelines' },
    { label: 'Running pipeline runs', key: 'runningPipelineRuns', icon: 'play_circle', color: '#2563eb', route: '/pipelines' },
    { label: 'Waiting approval', key: 'waitingApprovalRuns', icon: 'approval', color: '#d97706', route: '/pipelines' },
    { label: 'Pending approvals', key: 'pendingApprovals', icon: 'notifications', color: '#ea580c', route: '/pipelines' },
    { label: 'Failed runs', key: 'failedPipelineRuns', icon: 'error', color: '#dc2626', route: '/pipelines' },
    { label: 'Completed runs', key: 'completedPipelineRuns', icon: 'check_circle', color: '#16a34a', route: '/pipelines' },
    { label: 'Features in progress', key: 'featuresInProgress', icon: 'pending_actions', color: '#2563eb', route: '/features' },
    { label: 'Total features', key: 'totalFeatures', icon: 'inventory_2', color: '#6b7280', route: '/features' },
    { label: 'Catalog entries', key: 'totalCatalogEntries', icon: 'menu_book', color: '#7c3aed', route: '/catalog' },
    { label: 'Broken catalog entries', key: 'brokenCatalogEntries', icon: 'report', color: '#dc2626', route: '/catalog' },
  ];

  constructor() {
    this.refreshSummary();
    this.refreshRuns();

    this.signalR.pipelineRunUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.refreshSummary();
      this.refreshRuns();
    });
    this.signalR.stepRunUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refreshRuns());
    this.signalR.approvalRequested$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.refreshSummary();
      this.refreshRuns();
    });
    this.signalR.approvalDecided$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.refreshSummary();
      this.refreshRuns();
    });
    this.signalR.repositoryUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refreshSummary());
    this.signalR.catalogRefreshed$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refreshSummary());
  }

  private refreshSummary(): void {
    this.dashboard.summary().subscribe({
      next: (s) => this.summary.set(s),
      error: (err) => console.error('summary failed', err),
    });
  }

  private refreshRuns(): void {
    this.pipelineRuns.list().subscribe({
      next: (list) => this.recentRuns.set(list.slice(0, 10)),
      error: (err) => console.error('pipeline runs failed', err),
    });
  }
}
