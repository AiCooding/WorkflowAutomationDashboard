import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { CommandsService } from '../../core/api/commands.service';
import { FeaturesService } from '../../core/api/features.service';
import { WorkflowsService } from '../../core/api/workflows.service';
import { Command, Feature, Workflow, WorkflowType } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';
import { StatusStyle } from '../../shared/status-style';

const WORKFLOW_TYPES: WorkflowType[] = ['full-pipeline', 'bugfix', 'review-only', 'feature-spec', 'custom'];

@Component({
  selector: 'app-control',
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatTableModule,
  ],
  templateUrl: './control.html',
  styleUrl: './control.scss',
})
export class ControlPage {
  private readonly workflowsApi = inject(WorkflowsService);
  private readonly featuresApi = inject(FeaturesService);
  private readonly commandsApi = inject(CommandsService);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly snackBar = inject(MatSnackBar);
  readonly styles = inject(StatusStyle);

  readonly workflowTypes = WORKFLOW_TYPES;
  readonly features = signal<Feature[]>([]);
  readonly recentCommands = signal<Command[]>([]);
  readonly runningWorkflows = signal<Workflow[]>([]);

  readonly form = signal<{ type: WorkflowType; featureId: string; payload: string }>({
    type: 'full-pipeline',
    featureId: '',
    payload: '',
  });
  readonly starting = signal(false);
  readonly bulkBusy = signal(false);

  readonly draft = signal<{ name: string; description: string }>({ name: '', description: '' });
  readonly drafting = signal(false);

  readonly commandColumns = ['commandType', 'workflowId', 'status', 'createdAt', 'processedAt'];

  constructor() {
    this.featuresApi.list().subscribe({ next: (list) => this.features.set(list) });
    this.refreshCommands();
    this.refreshRunning();

    this.signalR.commandIssued$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((cmd) => {
        this.recentCommands.update((list) => [cmd, ...list].slice(0, 20));
      });

    this.signalR.workflowUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshRunning());
  }

  refreshCommands(): void {
    this.commandsApi.list().subscribe({
      next: (list) => this.recentCommands.set(list.slice(0, 20)),
    });
  }

  refreshRunning(): void {
    this.workflowsApi.list({ status: 'running' }).subscribe({
      next: (list) => this.runningWorkflows.set(list),
    });
  }

  updateForm<K extends 'type' | 'featureId' | 'payload'>(
    key: K,
    value: K extends 'type' ? WorkflowType : string,
  ): void {
    this.form.update((f) => ({ ...f, [key]: value }));
  }

  updateDraft<K extends 'name' | 'description'>(key: K, value: string): void {
    this.draft.update((d) => ({ ...d, [key]: value }));
  }

  startDraft(): void {
    const d = this.draft();
    if (!d.description.trim()) {
      this.snackBar.open('Please describe what you want to build', 'Dismiss', { duration: 2500 });
      return;
    }
    this.drafting.set(true);

    this.workflowsApi
      .create({ type: 'feature-spec', status: 'pending' })
      .subscribe({
        next: (workflow) => {
          const payload = JSON.stringify({
            name: d.name.trim() || null,
            description: d.description.trim(),
          });
          this.commandsApi
            .create({
              workflowId: workflow.id,
              commandType: 'start',
              payloadJson: payload,
              status: 'pending',
            })
            .subscribe({
              next: () => {
                this.drafting.set(false);
                this.snackBar.open(
                  `Feature-spec workflow ${workflow.id} created. The PM agent will pick it up.`,
                  'Dismiss',
                  { duration: 3500 },
                );
                this.draft.set({ name: '', description: '' });
                this.refreshRunning();
                this.refreshCommands();
              },
              error: () => {
                this.drafting.set(false);
                this.snackBar.open('Workflow created but command failed', 'Dismiss', { duration: 3000 });
              },
            });
        },
        error: () => {
          this.drafting.set(false);
          this.snackBar.open('Failed to create draft workflow', 'Dismiss', { duration: 3000 });
        },
      });
  }

  start(): void {
    const f = this.form();
    this.starting.set(true);

    this.workflowsApi
      .create({
        type: f.type,
        featureId: f.featureId || null,
        status: 'pending',
      })
      .subscribe({
        next: (workflow) => {
          // If payload provided, issue a start command with payload
          if (f.payload?.trim()) {
            this.commandsApi
              .create({
                workflowId: workflow.id,
                commandType: 'start',
                payloadJson: f.payload.trim(),
                status: 'pending',
              })
              .subscribe();
          } else {
            this.commandsApi
              .create({
                workflowId: workflow.id,
                commandType: 'start',
                status: 'pending',
              })
              .subscribe();
          }
          this.starting.set(false);
          this.snackBar.open(`Workflow ${workflow.id} started`, 'Dismiss', { duration: 2500 });
          this.form.set({ type: 'full-pipeline', featureId: '', payload: '' });
          this.refreshRunning();
        },
        error: () => {
          this.starting.set(false);
          this.snackBar.open('Failed to start workflow', 'Dismiss', { duration: 3000 });
        },
      });
  }

  bulk(action: 'cancel' | 'pause'): void {
    const targets = this.runningWorkflows();
    if (targets.length === 0) {
      this.snackBar.open('No running workflows', 'Dismiss', { duration: 2000 });
      return;
    }
    if (!confirm(`${action === 'cancel' ? 'Cancel' : 'Pause'} all ${targets.length} running workflow(s)?`)) {
      return;
    }
    this.bulkBusy.set(true);

    let done = 0;
    targets.forEach((w) => {
      this.commandsApi
        .create({
          workflowId: w.id,
          commandType: action,
          status: 'pending',
        })
        .subscribe({
          next: () => {
            done++;
            if (done === targets.length) {
              this.bulkBusy.set(false);
              this.snackBar.open(`${action} command sent to ${done} workflow(s)`, 'Dismiss', { duration: 2500 });
            }
          },
          error: () => {
            done++;
            if (done === targets.length) this.bulkBusy.set(false);
          },
        });
    });
  }
}
