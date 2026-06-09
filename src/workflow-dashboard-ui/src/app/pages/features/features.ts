import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FeaturesService } from '../../core/api/features.service';
import { Feature, FeatureSpec, FeatureStatus } from '../../core/models';
import { StatusStyle } from '../../shared/status-style';

const STATUSES: FeatureStatus[] = ['backlog', 'planning', 'in_progress', 'review', 'done', 'cancelled'];

interface NewFeatureForm {
  id: string;
  name: string;
  description: string;
  status: FeatureStatus;
  priority: number;
}

@Component({
  selector: 'app-features',
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './features.html',
  styleUrl: './features.scss',
})
export class FeaturesPage {
  private readonly featuresApi = inject(FeaturesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly snackBar = inject(MatSnackBar);
  readonly styles = inject(StatusStyle);

  readonly statuses = STATUSES;
  readonly columns = ['name', 'status', 'priority', 'updatedAt', 'actions'];

  readonly features = signal<Feature[]>([]);
  readonly loading = signal(true);
  readonly showCreateForm = signal(false);
  readonly saving = signal(false);

  readonly newFeature = signal<NewFeatureForm>(this.emptyForm());

  // Spec viewer state
  readonly viewingSpec = signal<FeatureSpec | null>(null);
  readonly viewingFeatureId = signal<string | null>(null);
  readonly specLoading = signal(false);
  readonly specError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  private emptyForm(): NewFeatureForm {
    return { id: '', name: '', description: '', status: 'backlog', priority: 0 };
  }

  private load(): void {
    this.loading.set(true);
    this.featuresApi
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.features.set(list);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.snackBar.open('Failed to load features', 'Dismiss', { duration: 3000 });
        },
      });
  }

  toggleCreate(): void {
    this.showCreateForm.update((v) => !v);
    if (!this.showCreateForm()) this.newFeature.set(this.emptyForm());
  }

  saveNew(): void {
    const form = this.newFeature();
    if (!form.name.trim()) {
      this.snackBar.open('Name is required', 'Dismiss', { duration: 2500 });
      return;
    }
    this.saving.set(true);
    this.featuresApi
      .create({
        id: form.id.trim() || undefined,
        name: form.name.trim(),
        description: form.description.trim() || null,
        status: form.status,
        priority: form.priority,
      })
      .subscribe({
        next: (created) => {
          this.features.update((list) => [created, ...list]);
          this.saving.set(false);
          this.showCreateForm.set(false);
          this.newFeature.set(this.emptyForm());
          this.snackBar.open('Feature created', 'Dismiss', { duration: 2000 });
        },
        error: (err) => {
          this.saving.set(false);
          this.snackBar.open('Create failed: ' + (err?.message || 'unknown'), 'Dismiss', { duration: 4000 });
        },
      });
  }

  updateStatus(feature: Feature, status: FeatureStatus): void {
    if (feature.status === status) return;
    const updated = { ...feature, status };
    this.featuresApi.update(feature.id, updated).subscribe({
      next: (saved) => {
        this.features.update((list) => list.map((f) => (f.id === saved.id ? saved : f)));
      },
      error: () => this.snackBar.open('Update failed', 'Dismiss', { duration: 3000 }),
    });
  }

  remove(feature: Feature): void {
    if (!confirm(`Delete feature "${feature.name}"?`)) return;
    this.featuresApi.delete(feature.id).subscribe({
      next: () => {
        this.features.update((list) => list.filter((f) => f.id !== feature.id));
        if (this.viewingFeatureId() === feature.id) this.closeSpec();
        this.snackBar.open('Feature deleted', 'Dismiss', { duration: 2000 });
      },
      error: () => this.snackBar.open('Delete failed', 'Dismiss', { duration: 3000 }),
    });
  }

  viewSpec(feature: Feature): void {
    if (this.viewingFeatureId() === feature.id) {
      this.closeSpec();
      return;
    }
    this.viewingFeatureId.set(feature.id);
    this.viewingSpec.set(null);
    this.specLoading.set(true);
    this.specError.set(null);

    this.featuresApi.getSpec(feature.id).subscribe({
      next: (spec) => {
        this.viewingSpec.set(spec);
        this.specLoading.set(false);
      },
      error: (err) => {
        this.specLoading.set(false);
        this.specError.set(err?.error?.message || err?.message || 'Failed to load spec');
      },
    });
  }

  closeSpec(): void {
    this.viewingFeatureId.set(null);
    this.viewingSpec.set(null);
    this.specError.set(null);
  }

  updateForm<K extends keyof NewFeatureForm>(key: K, value: NewFeatureForm[K]): void {
    this.newFeature.update((f) => ({ ...f, [key]: value }));
  }
}
