import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CatalogService } from '../../core/api/catalog.service';
import { PipelinesService } from '../../core/api/pipelines.service';
import { CatalogEntry, PipelineStepDef } from '../../core/models';

interface PipelineFormModel {
  name: string;
  description: string;
  steps: PipelineStepDef[];
}

@Component({
  selector: 'app-pipeline-designer',
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './pipeline-designer.html',
  styleUrl: './pipeline-designer.scss',
})
export class PipelineDesignerPage {
  private readonly pipelinesApi = inject(PipelinesService);
  private readonly catalogApi = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly pipelineId = signal<string | null>(null);
  readonly agents = signal<CatalogEntry[]>([]);
  readonly model = signal<PipelineFormModel>({
    name: '',
    description: '',
    steps: [],
  });

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    this.pipelineId.set(id);

    this.catalogApi.list().subscribe({
      next: (entries) => this.agents.set(entries.filter((entry) => entry.kind === 'agent' && !entry.isBroken)),
    });

    if (id) {
      this.pipelinesApi.get(id).subscribe({
        next: (pipeline) => {
          this.model.set({
            name: pipeline.name,
            description: pipeline.description ?? '',
            steps: this.parseSteps(pipeline.stepsJson),
          });
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.snackBar.open('Failed to load pipeline', 'Dismiss', { duration: 3000 });
        },
      });
    } else {
      this.addStep('agent');
      this.loading.set(false);
    }
  }

  addStep(type: 'agent' | 'userApproval'): void {
    const index = this.model().steps.length + 1;
    const nextStep: PipelineStepDef = {
      id: `step-${index}`,
      type,
      name: type === 'agent' ? `Agent step ${index}` : `Approval step ${index}`,
      agentSlug: type === 'agent' ? this.agents()[0]?.slug ?? null : null,
      canGiveFeedback: type === 'userApproval',
      returnTo: null,
    };

    this.model.update((model) => ({
      ...model,
      steps: [...model.steps, nextStep],
    }));
  }

  removeStep(index: number): void {
    this.model.update((model) => ({
      ...model,
      steps: model.steps.filter((_, stepIndex) => stepIndex !== index),
    }));
  }

  moveStep(index: number, offset: number): void {
    const steps = [...this.model().steps];
    const target = index + offset;
    if (target < 0 || target >= steps.length) return;
    const [item] = steps.splice(index, 1);
    steps.splice(target, 0, item);
    this.model.update((model) => ({ ...model, steps }));
  }

  updateField<K extends keyof PipelineFormModel>(key: K, value: PipelineFormModel[K]): void {
    this.model.update((model) => ({ ...model, [key]: value }));
  }

  updateStep(index: number, patch: Partial<PipelineStepDef>): void {
    this.model.update((model) => ({
      ...model,
      steps: model.steps.map((step, stepIndex) => {
        if (stepIndex !== index) return step;
        const next = { ...step, ...patch };
        // When switching to agent: clear agentSlug only if not already set
        if (next.type === 'agent') {
          next.agentSlug = next.agentSlug || this.agents()[0]?.slug || null;
        }
        // When switching to userApproval: clear agentSlug
        if (next.type === 'userApproval') {
          next.agentSlug = null;
        }
        return next;
      }),
    }));
  }

  stepsJsonPreview(): string {
    return JSON.stringify({ steps: this.model().steps }, null, 2);
  }

  save(): void {
    const model = this.model();
    if (!model.name.trim()) {
      this.snackBar.open('Pipeline name is required', 'Dismiss', { duration: 2500 });
      return;
    }
    if (model.steps.length === 0) {
      this.snackBar.open('Add at least one pipeline step', 'Dismiss', { duration: 2500 });
      return;
    }
    if (model.steps.some((step) => !step.id.trim() || !step.name.trim())) {
      this.snackBar.open('Every step needs an ID and name', 'Dismiss', { duration: 3000 });
      return;
    }
    if (model.steps.some((step) => step.type === 'agent' && !step.agentSlug)) {
      this.snackBar.open('Every agent step must select an agent', 'Dismiss', { duration: 3000 });
      return;
    }

    const body = {
      name: model.name.trim(),
      description: model.description.trim() || null,
      stepsJson: JSON.stringify({ steps: model.steps }),
    };

    this.saving.set(true);
    const request = this.pipelineId()
      ? this.pipelinesApi.update(this.pipelineId()!, body)
      : this.pipelinesApi.create(body);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.snackBar.open('Pipeline saved', 'Dismiss', { duration: 2000 });
        void this.router.navigate(['/pipelines']);
      },
      error: (err) => {
        this.saving.set(false);
        const message = err?.error?.message ?? err?.message ?? 'Failed to save pipeline';
        this.snackBar.open(message, 'Dismiss', { duration: 4000 });
      },
    });
  }

  private parseSteps(stepsJson: string): PipelineStepDef[] {
    try {
      const parsed = JSON.parse(stepsJson || '{}');
      return Array.isArray(parsed?.steps) ? parsed.steps : [];
    } catch {
      return [];
    }
  }

  exportPipeline(): void {
    const id = this.pipelineId();
    if (!id) {
      this.snackBar.open('Save the pipeline first before exporting.', 'Dismiss', { duration: 2500 });
      return;
    }
    this.pipelinesApi.exportPipeline(id).subscribe({
      next: (dto) => {
        const blob = new Blob([JSON.stringify(dto, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${dto.name.replace(/\s+/g, '-').toLowerCase()}.pipeline.json`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.snackBar.open('Export failed', 'Dismiss', { duration: 3000 }),
    });
  }
}
