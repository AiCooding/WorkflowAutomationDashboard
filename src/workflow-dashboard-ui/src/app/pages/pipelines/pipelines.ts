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
import { FeaturesService } from '../../core/api/features.service';
import { PipelineRunsService } from '../../core/api/pipeline-runs.service';
import { PipelinesService } from '../../core/api/pipelines.service';
import { RepositoriesService } from '../../core/api/repositories.service';
import { ApprovalRequest, Feature, Pipeline, PipelineRun, PipelineStepDef, PipelineStepRun, Repository } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';
import { StatusStyle, formatDuration } from '../../shared/status-style';

interface RunFormState {
  open: boolean;
  repositoryId: string;
  featureId: string;
}

interface StepLogLine {
  stepRunId: string;
  stream: 'stdout' | 'stderr';
  line: string;
  ts: string;
}

interface LogPanelState {
  lines: StepLogLine[];
  liveSub?: Subscription;
  tailSub?: Subscription;
}

@Component({
  selector: 'app-pipelines',
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterLink,
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
  ],
  templateUrl: './pipelines.html',
  styleUrl: './pipelines.scss',
})
export class PipelinesPage {
  private readonly pipelinesApi = inject(PipelinesService);
  private readonly pipelineRunsApi = inject(PipelineRunsService);
  private readonly repositoriesApi = inject(RepositoriesService);
  private readonly featuresApi = inject(FeaturesService);
  private readonly signalR = inject(SignalRService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  readonly styles = inject(StatusStyle);

  readonly loading = signal(true);
  readonly pipelines = signal<Pipeline[]>([]);
  readonly runs = signal<PipelineRun[]>([]);
  readonly repositories = signal<Repository[]>([]);
  readonly features = signal<Feature[]>([]);
  readonly runForms = signal<Record<string, RunFormState>>({});
  readonly approvalFeedback = signal<Record<string, string>>({});
  readonly logs = signal<Record<string, LogPanelState>>({});
  readonly busyPipelineId = signal<string | null>(null);
  readonly busyRunId = signal<string | null>(null);

  readonly pipelineMap = computed(() => new Map(this.pipelines().map((p) => [p.id, p])));

  constructor() {
    this.reload();

    this.signalR.pipelineRunUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadRuns());
    this.signalR.stepRunUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadRuns());
    this.signalR.approvalRequested$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadRuns());
    this.signalR.approvalDecided$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadRuns());
  }

  reload(): void {
    this.loading.set(true);
    this.loadPipelines();
    this.loadRuns();
    this.repositoriesApi.list().subscribe({ next: (repos) => this.repositories.set(repos) });
    this.featuresApi.list().subscribe({ next: (features) => this.features.set(features) });
  }

  private loadPipelines(): void {
    this.pipelinesApi.list().subscribe({
      next: (pipelines) => {
        this.pipelines.set(pipelines.map((p) => this.normalizePipeline(p)));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load pipelines', 'Dismiss', { duration: 3000 });
      },
    });
  }

  private loadRuns(): void {
    this.pipelineRunsApi.list().subscribe({
      next: (runs) => this.runs.set(runs),
      error: () => this.snackBar.open('Failed to load pipeline runs', 'Dismiss', { duration: 3000 }),
    });
  }

  toggleRunForm(pipeline: Pipeline): void {
    this.runForms.update((state) => {
      const current = state[pipeline.id];
      const firstRepo = this.repositories().find((r) => !r.isBroken)?.id ?? '';
      return {
        ...state,
        [pipeline.id]: {
          open: !current?.open,
          repositoryId: current?.repositoryId ?? firstRepo,
          featureId: current?.featureId ?? '',
        },
      };
    });
  }

  updateRunForm(pipelineId: string, key: 'repositoryId' | 'featureId', value: string): void {
    this.runForms.update((state) => ({
      ...state,
      [pipelineId]: {
        open: true,
        repositoryId: state[pipelineId]?.repositoryId ?? '',
        featureId: state[pipelineId]?.featureId ?? '',
        [key]: value,
      },
    }));
  }

  startRun(pipeline: Pipeline): void {
    const form = this.runForms()[pipeline.id];
    if (!form?.repositoryId) {
      this.snackBar.open('Select a repository before starting a pipeline.', 'Dismiss', { duration: 2500 });
      return;
    }

    this.busyPipelineId.set(pipeline.id);
    this.pipelineRunsApi.start({
      pipelineId: pipeline.id,
      repositoryId: form.repositoryId,
      featureId: form.featureId || null,
    }).subscribe({
      next: () => {
        this.busyPipelineId.set(null);
        this.toggleRunForm(pipeline);
        this.loadRuns();
        this.snackBar.open(`Pipeline ${pipeline.name} started`, 'Dismiss', { duration: 2500 });
      },
      error: (err) => {
        this.busyPipelineId.set(null);
        const message = err?.error?.message ?? err?.error ?? 'Failed to start pipeline';
        this.snackBar.open(String(message), 'Dismiss', { duration: 4000 });
      },
    });
  }

  deletePipeline(pipeline: Pipeline): void {
    if (!confirm(`Delete pipeline "${pipeline.name}"?`)) return;
    this.pipelinesApi.delete(pipeline.id).subscribe({
      next: () => {
        this.pipelines.update((list) => list.filter((item) => item.id !== pipeline.id));
        this.snackBar.open('Pipeline deleted', 'Dismiss', { duration: 2000 });
      },
      error: () => this.snackBar.open('Failed to delete pipeline', 'Dismiss', { duration: 3000 }),
    });
  }

  cancel(run: PipelineRun): void {
    if (!confirm(`Cancel pipeline run ${this.shortId(run.id)}?`)) return;
    this.busyRunId.set(run.id);
    this.pipelineRunsApi.cancel(run.id).subscribe({
      next: () => {
        this.busyRunId.set(null);
        this.snackBar.open('Cancel requested', 'Dismiss', { duration: 2000 });
      },
      error: (err) => {
        this.busyRunId.set(null);
        const message = err?.error?.message ?? err?.message ?? 'Cancel failed';
        this.snackBar.open(message, 'Dismiss', { duration: 4000 });
      },
    });
  }

  decide(run: PipelineRun, decision: 'approved' | 'rejected'): void {
    const approval = run.pendingApproval;
    if (!approval) return;

    const feedback = this.approvalFeedback()[approval.id] ?? '';
    this.busyRunId.set(run.id);
    this.pipelineRunsApi.decide(run.id, approval.id, {
      decision,
      feedbackText: feedback || null,
    }).subscribe({
      next: () => {
        this.busyRunId.set(null);
        this.approvalFeedback.update((state) => ({ ...state, [approval.id]: '' }));
        this.loadRuns();
        this.snackBar.open(`Approval ${decision}`, 'Dismiss', { duration: 2500 });
      },
      error: (err) => {
        this.busyRunId.set(null);
        const message = err?.error?.message ?? err?.message ?? 'Approval failed';
        this.snackBar.open(message, 'Dismiss', { duration: 4000 });
      },
    });
  }

  updateApprovalFeedback(approvalId: string, value: string): void {
    this.approvalFeedback.update((state) => ({ ...state, [approvalId]: value }));
  }

  stepsForRun(run: PipelineRun): PipelineStepDef[] {
    return this.pipelineMap().get(run.pipelineId)?.steps ?? [];
  }

  latestStepRun(run: PipelineRun, stepId: string): PipelineStepRun | undefined {
    return (run.stepRuns ?? [])
      .filter((stepRun) => stepRun.stepId === stepId)
      .sort((a, b) => (a.attemptNumber - b.attemptNumber) || (a.startedAt || '').localeCompare(b.startedAt || ''))
      .at(-1);
  }

  stepStatus(run: PipelineRun, step: PipelineStepDef): string {
    const latest = this.latestStepRun(run, step.id);
    if (latest?.status) return latest.status;
    if (run.currentStepId === step.id) return run.status;
    return 'pending';
  }

  stepName(run: PipelineRun, stepId?: string | null): string {
    if (!stepId) return '—';
    return this.stepsForRun(run).find((step) => step.id === stepId)?.name ?? stepId;
  }

  activeLogStep(run: PipelineRun): PipelineStepRun | undefined {
    const stepRuns = run.stepRuns ?? [];
    return stepRuns.find((stepRun) => stepRun.status === 'running')
      ?? [...stepRuns].sort((a, b) => (a.startedAt || '').localeCompare(b.startedAt || '')).at(-1);
  }

  logsFor(run: PipelineRun): StepLogLine[] {
    const stepRun = this.activeLogStep(run);
    return stepRun ? this.logs()[stepRun.id]?.lines ?? [] : [];
  }

  onPanelOpened(run: PipelineRun): void {
    const stepRun = this.activeLogStep(run);
    if (!stepRun) return;
    if (this.logs()[stepRun.id]) return;

    const next: LogPanelState = { lines: [] };
    this.logs.update((map) => ({ ...map, [stepRun.id]: next }));

    next.liveSub = this.signalR.stepLog$(stepRun.id).subscribe((line) => {
      const current = this.logs();
      const state = current[stepRun.id];
      if (!state) return;
      const lines = state.lines.concat(line);
      if (lines.length > 1000) lines.splice(0, lines.length - 1000);
      this.logs.update((map) => ({ ...map, [stepRun.id]: { ...state, lines } }));
    });

    next.tailSub = this.signalR.stepLogTail$(stepRun.id).subscribe((tail) => {
      const current = this.logs();
      const state = current[stepRun.id];
      if (!state) return;
      this.logs.update((map) => ({ ...map, [stepRun.id]: { ...state, lines: tail.concat(state.lines) } }));
    });

    void this.signalR.subscribeToStepRun(stepRun.id);
  }

  onPanelClosed(run: PipelineRun): void {
    const stepRun = this.activeLogStep(run);
    if (!stepRun) return;
    const state = this.logs()[stepRun.id];
    if (!state) return;

    state.liveSub?.unsubscribe();
    state.tailSub?.unsubscribe();
    void this.signalR.unsubscribeFromStepRun(stepRun.id);
    this.logs.update((map) => {
      const next = { ...map };
      delete next[stepRun.id];
      return next;
    });
  }

  approvalStep(run: PipelineRun): ApprovalRequest | null {
    return run.pendingApproval ?? null;
  }

  canGiveFeedback(run: PipelineRun): boolean {
    const approval = run.pendingApproval;
    if (!approval) return false;
    return !!this.stepsForRun(run).find((step) => step.id === approval.stepId)?.canGiveFeedback;
  }

  duration(run: PipelineRun): string {
    return formatDuration(run.startedAt, run.completedAt);
  }

  shortId(id: string): string {
    return id.length > 8 ? id.slice(0, 8) : id;
  }

  runBusy(runId: string): boolean {
    return this.busyRunId() === runId;
  }

  pipelineBusy(pipelineId: string): boolean {
    return this.busyPipelineId() === pipelineId;
  }

  private normalizePipeline(pipeline: Pipeline): Pipeline {
    return {
      ...pipeline,
      steps: this.parseSteps(pipeline.stepsJson),
    };
  }

  private parseSteps(stepsJson: string): PipelineStepDef[] {
    try {
      const parsed = JSON.parse(stepsJson || '{}');
      return Array.isArray(parsed?.steps) ? parsed.steps : [];
    } catch {
      return [];
    }
  }
}
