import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { RepositoriesService } from '../../core/api/repositories.service';
import { RunsService } from '../../core/api/runs.service';
import { WorkflowsService } from '../../core/api/workflows.service';
import { Repository, Workflow, WorkflowLogLine, WorkflowStatus } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';
import { StatusStyle, formatDuration } from '../../shared/status-style';

const STATUSES: WorkflowStatus[] = [
  'pending', 'queued', 'running', 'completed', 'failed', 'cancelled', 'broken',
];

interface LogPanelState {
  lines: WorkflowLogLine[];
  liveSub?: Subscription;
  tailSub?: Subscription;
}

@Component({
  selector: 'app-runs',
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatTooltipModule,
    RouterLink,
  ],
  templateUrl: './runs.html',
  styleUrl: './runs.scss',
})
export class RunsPage {
  private readonly workflowsApi = inject(WorkflowsService);
  private readonly runsApi = inject(RunsService);
  private readonly repositoriesApi = inject(RepositoriesService);
  private readonly signalR = inject(SignalRService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  readonly styles = inject(StatusStyle);

  readonly statuses = STATUSES;
  readonly statusFilter = signal<WorkflowStatus | ''>('');
  readonly repositoryFilter = signal<string>('');
  readonly featureFilter = signal<string>('');

  readonly loading = signal(true);
  readonly all = signal<Workflow[]>([]);
  /** Kept for the Repository filter dropdown. Display names come from the DTO. */
  readonly repositories = signal<Repository[]>([]);
  readonly logs = signal<Record<string, LogPanelState>>({});

  readonly filtered = computed(() => {
    const status = this.statusFilter();
    const repoId = this.repositoryFilter();
    const feature = this.featureFilter().trim().toLowerCase();
    return this.all().filter((w) => {
      if (status && w.status !== status) return false;
      if (repoId && w.repositoryId !== repoId) return false;
      if (feature) {
        // Match on feature name (Phase 6 joined field) OR feature id.
        const nameMatch = (w.featureName || '').toLowerCase().includes(feature);
        const idMatch = (w.featureId || '').toLowerCase().includes(feature);
        if (!nameMatch && !idMatch) return false;
      }
      return true;
    });
  });

  constructor() {
    this.load();
    this.repositoriesApi.list().subscribe({ next: (list) => this.repositories.set(list) });

    this.signalR.workflowUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((w) => this.upsertWorkflow(w));
  }

  load(): void {
    this.loading.set(true);
    this.workflowsApi
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.all.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  private upsertWorkflow(w: Workflow): void {
    const existing = this.all();
    const idx = existing.findIndex((x) => x.id === w.id);
    if (idx >= 0) {
      // For existing entries: spread merge. The SignalR payload is the raw entity
      // (no joined fields), so spreading it over the existing DTO preserves
      // catalogDisplayName / featureName / repositoryName / repositoryPath that
      // were loaded from GET /api/workflows — as long as those keys are absent
      // from the SignalR payload (which they are, since it sends Workflow not WorkflowDto).
      const next = existing.slice();
      next[idx] = { ...next[idx], ...w };
      this.all.set(next);
    } else {
      // New workflow arrived via SignalR: fetch the enriched DTO so the row
      // immediately shows catalog display name, feature name, and repository.
      this.workflowsApi
        .get(w.id)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (enriched) => this.all.update((list) => [enriched, ...list]),
          error: () => this.all.update((list) => [w, ...list]), // graceful fallback
        });
    }
  }

  /** Human-readable display label for the Type column (Phase 6). */
  typeLabel(w: Workflow): string {
    return w.catalogDisplayName ?? w.catalogSlug;
  }

  /** True when the workflow's catalog definition no longer exists. */
  definitionMissing(w: Workflow): boolean {
    return w.catalogDisplayName === null;
  }

  /** Display label for the Repository column. */
  repoLabel(w: Workflow): string {
    return w.repositoryName ?? w.repositoryPath ?? w.repositoryId ?? '—';
  }

  /** Display label for the Feature column. */
  featureLabel(w: Workflow): string {
    return w.featureName ?? w.featureId ?? '—';
  }

  duration(w: Workflow): string {
    return formatDuration(w.startedAt, w.completedAt);
  }

  shortId(id: string): string {
    return id.length > 8 ? id.slice(0, 8) : id;
  }

  cancellable(w: Workflow): boolean {
    return w.status === 'pending' || w.status === 'queued' || w.status === 'running';
  }

  requeueable(w: Workflow): boolean {
    return w.status === 'broken';
  }

  cancel(w: Workflow): void {
    if (!confirm(`Cancel workflow ${this.shortId(w.id)}?`)) return;
    this.runsApi.cancel(w.id).subscribe({
      next: () => this.snackBar.open('Cancel requested', 'Dismiss', { duration: 2000 }),
      error: (err) => this.snackBar.open(`Cancel failed: ${err?.error?.message ?? err.message}`, 'Dismiss', { duration: 4000 }),
    });
  }

  requeue(w: Workflow): void {
    this.runsApi.requeue(w.id).subscribe({
      next: (updated) => {
        this.upsertWorkflow(updated);
        this.snackBar.open('Workflow re-queued', 'Dismiss', { duration: 2000 });
      },
      error: (err) => {
        const reason = err?.error?.brokenReason ?? err?.error?.message ?? err.message;
        this.snackBar.open(`Re-queue failed: ${reason}`, 'Dismiss', { duration: 5000 });
      },
    });
  }

  cancelAll(): void {
    if (!confirm('Cancel ALL pending, queued, and running workflows?')) return;
    this.runsApi.cancelAll().subscribe({
      next: (r) => this.snackBar.open(`Cancelled ${r.cancelled} workflow(s)`, 'Dismiss', { duration: 3000 }),
      error: (err) => this.snackBar.open(`Cancel-all failed: ${err?.error?.message ?? err.message}`, 'Dismiss', { duration: 4000 }),
    });
  }

  logsFor(id: string): WorkflowLogLine[] {
    return this.logs()[id]?.lines ?? [];
  }

  onPanelOpened(w: Workflow): void {
    const state = this.logs()[w.id];
    if (state) return; // already subscribed

    const next: LogPanelState = { lines: [] };
    this.logs.update((m) => ({ ...m, [w.id]: next }));

    // Subscribe to live + tail BEFORE invoking SubscribeToWorkflow so the tail snapshot is captured.
    next.liveSub = this.signalR.workflowLog$(w.id).subscribe((line) => {
      const cur = this.logs();
      const s = cur[w.id];
      if (!s) return;
      const lines = s.lines.concat(line);
      // Cap UI buffer to 1000 entries to keep DOM small.
      if (lines.length > 1000) lines.splice(0, lines.length - 1000);
      this.logs.update((m) => ({ ...m, [w.id]: { ...s, lines } }));
    });
    next.tailSub = this.signalR.workflowLogTail$(w.id).subscribe((tail) => {
      const cur = this.logs();
      const s = cur[w.id];
      if (!s) return;
      this.logs.update((m) => ({ ...m, [w.id]: { ...s, lines: tail.concat(s.lines) } }));
    });

    void this.signalR.subscribeToWorkflow(w.id);
  }

  onPanelClosed(w: Workflow): void {
    const state = this.logs()[w.id];
    if (!state) return;
    state.liveSub?.unsubscribe();
    state.tailSub?.unsubscribe();
    void this.signalR.unsubscribeFromWorkflow(w.id);
    this.logs.update((m) => {
      const next = { ...m };
      delete next[w.id];
      return next;
    });
  }
}
