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
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { WorkflowsService } from '../../core/api/workflows.service';
import { InputRequest, Workflow, WorkflowStatus } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';
import { formatDuration, StatusStyle } from '../../shared/status-style';

const STATUSES: WorkflowStatus[] = [
  'pending', 'running', 'paused', 'waiting_input', 'completed', 'failed', 'cancelled',
];

interface DialogState {
  loading: boolean;
  inputs: InputRequest[];
}

@Component({
  selector: 'app-workflows',
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    MatExpansionModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './workflows.html',
  styleUrl: './workflows.scss',
})
export class WorkflowsPage {
  private readonly workflowsApi = inject(WorkflowsService);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);
  readonly styles = inject(StatusStyle);

  readonly statuses = STATUSES;
  readonly statusFilter = signal<WorkflowStatus | ''>('');
  readonly featureFilter = signal<string>('');
  readonly loading = signal(true);
  readonly all = signal<Workflow[]>([]);

  // workflow id → dialog state (lazy-loaded on expand)
  readonly dialogs = signal<Record<string, DialogState>>({});

  readonly filtered = computed(() => {
    const status = this.statusFilter();
    const feature = this.featureFilter().trim().toLowerCase();
    return this.all().filter((w) => {
      if (status && w.status !== status) return false;
      if (feature && !(w.featureId || '').toLowerCase().includes(feature)) return false;
      return true;
    });
  });

  constructor() {
    this.load();

    this.signalR.inputAnswered$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((ir) => this.upsertDialogInput(ir));
    this.signalR.inputRequested$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((ir) => this.upsertDialogInput(ir));
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

  duration(w: Workflow): string {
    return formatDuration(w.startedAt, w.completedAt);
  }

  /** Lazy-load the full workflow (with input requests) when its panel is opened. */
  onPanelOpened(workflow: Workflow): void {
    const current = this.dialogs()[workflow.id];
    if (current && !current.loading) return;
    this.dialogs.update((map) => ({ ...map, [workflow.id]: { loading: true, inputs: [] } }));

    this.workflowsApi.get(workflow.id).subscribe({
      next: (full) => {
        const inputs = (full.inputRequests || []).slice().sort((a, b) =>
          a.createdAt.localeCompare(b.createdAt),
        );
        this.dialogs.update((map) => ({ ...map, [workflow.id]: { loading: false, inputs } }));
      },
      error: () => {
        this.dialogs.update((map) => ({ ...map, [workflow.id]: { loading: false, inputs: [] } }));
      },
    });
  }

  private upsertDialogInput(ir: InputRequest): void {
    const state = this.dialogs()[ir.workflowId];
    if (!state) return;
    const next = state.inputs.filter((x) => x.id !== ir.id).concat(ir);
    next.sort((a, b) => a.createdAt.localeCompare(b.createdAt));
    this.dialogs.update((map) => ({ ...map, [ir.workflowId]: { loading: false, inputs: next } }));
  }

  dialogFor(id: string): DialogState | undefined {
    return this.dialogs()[id];
  }

  cancel(w: Workflow): void {
    if (!confirm(`Cancel workflow ${w.id}?`)) return;
    this.workflowsApi.updateStatus(w.id, { status: 'cancelled' }).subscribe({
      next: (updated) => this.all.update((list) => list.map((x) => (x.id === updated.id ? updated : x))),
    });
  }

  pause(w: Workflow): void {
    this.workflowsApi.updateStatus(w.id, { status: 'paused' }).subscribe({
      next: (updated) => this.all.update((list) => list.map((x) => (x.id === updated.id ? updated : x))),
    });
  }

  resume(w: Workflow): void {
    this.workflowsApi.updateStatus(w.id, { status: 'running' }).subscribe({
      next: (updated) => this.all.update((list) => list.map((x) => (x.id === updated.id ? updated : x))),
    });
  }
}
