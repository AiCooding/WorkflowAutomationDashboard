import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FeaturesService } from '../../core/api/features.service';
import { RepositoriesService } from '../../core/api/repositories.service';
import { Feature, FeatureStatus, Repository } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';
import { StatusStyle } from '../../shared/status-style';
import { NewFeatureDialog } from './new-feature-dialog';
import { SpecViewer } from './spec-viewer';

const STATUSES: FeatureStatus[] = ['backlog', 'planning', 'in_progress', 'review', 'done', 'cancelled'];

const ORPHAN_FILTER = '__orphan__';
const ALL_FILTER = '__all__';

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
    MatDialogModule,
    SpecViewer,
  ],
  templateUrl: './features.html',
  styleUrl: './features.scss',
})
export class FeaturesPage {
  private readonly featuresApi = inject(FeaturesService);
  private readonly reposApi = inject(RepositoriesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly signalR = inject(SignalRService);
  readonly styles = inject(StatusStyle);

  readonly statuses = STATUSES;
  readonly columns = ['name', 'repository', 'status', 'priority', 'updatedAt', 'actions'];

  readonly features = signal<Feature[]>([]);
  readonly repositories = signal<Repository[]>([]);
  readonly loading = signal(true);
  readonly viewingFeatureId = signal<string | null>(null);
  readonly orphanFilter = ORPHAN_FILTER;
  readonly allFilter = ALL_FILTER;
  readonly repoFilter = signal<string>(ALL_FILTER);

  readonly visibleFeatures = computed(() => {
    const filter = this.repoFilter();
    const all = this.features();
    if (filter === ALL_FILTER) return all;
    if (filter === ORPHAN_FILTER) return all.filter((f) => !f.repositoryId);
    return all.filter((f) => f.repositoryId === filter);
  });

  readonly repoNameById = computed(() => {
    const map = new Map<string, string>();
    for (const r of this.repositories()) map.set(r.id, r.name);
    return map;
  });

  constructor() {
    this.load();

    // Live updates from SignalR — create, update, delete, and orphan flips.
    this.signalR.featureUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((f) => this.applyFeatureEvent(f));

    this.signalR.repositoryUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((r) => {
        // Keep the filter dropdown in sync; deletions arrive with deleted:true.
        this.repositories.update((list) => {
          if (r.deleted) return list.filter((x) => x.id !== r.id);
          const idx = list.findIndex((x) => x.id === r.id);
          if (idx === -1) return [r, ...list];
          const next = list.slice();
          next[idx] = r;
          return next;
        });
      });
  }

  private load(): void {
    this.loading.set(true);
    this.featuresApi.list().subscribe({
      next: (list) => {
        this.features.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load features', 'Dismiss', { duration: 3000 });
      },
    });
    this.reposApi.list().subscribe({
      next: (list) => this.repositories.set(list),
      error: () => undefined,
    });
  }

  private applyFeatureEvent(f: Feature): void {
    this.features.update((list) => {
      if (f.deleted) return list.filter((x) => x.id !== f.id);
      const idx = list.findIndex((x) => x.id === f.id);
      if (idx === -1) return [f, ...list];
      const next = list.slice();
      next[idx] = f;
      return next;
    });
    if (f.deleted && this.viewingFeatureId() === f.id) this.viewingFeatureId.set(null);
  }

  openNewFeatureDialog(): void {
    const ref = this.dialog.open<NewFeatureDialog, void, Feature | null>(NewFeatureDialog, {
      width: '720px',
      autoFocus: 'first-tabbable',
    });
    ref.afterClosed().subscribe((created) => {
      if (!created) return;
      this.applyFeatureEvent(created);
      this.snackBar.open(`Feature created — ${created.name}`, 'Dismiss', { duration: 2500 });
    });
  }

  updateStatus(feature: Feature, status: FeatureStatus): void {
    if (feature.status === status) return;
    this.featuresApi.update(feature.id, { status }).subscribe({
      next: (saved) => this.applyFeatureEvent(saved),
      error: () => this.snackBar.open('Update failed', 'Dismiss', { duration: 3000 }),
    });
  }

  remove(feature: Feature): void {
    if (!confirm(`Delete feature "${feature.name}"?`)) return;
    this.featuresApi.delete(feature.id).subscribe({
      next: () => {
        this.applyFeatureEvent({ ...feature, deleted: true });
        this.snackBar.open('Feature deleted', 'Dismiss', { duration: 2000 });
      },
      error: () => this.snackBar.open('Delete failed', 'Dismiss', { duration: 3000 }),
    });
  }

  viewSpec(feature: Feature): void {
    if (this.viewingFeatureId() === feature.id) {
      this.viewingFeatureId.set(null);
      return;
    }
    this.viewingFeatureId.set(feature.id);
  }

  closeSpec(): void {
    this.viewingFeatureId.set(null);
  }

  repoName(id?: string | null): string {
    if (!id) return '—';
    return this.repoNameById().get(id) ?? id;
  }
}
