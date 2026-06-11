import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { FeaturesService } from '../../core/api/features.service';
import { RepositoriesService } from '../../core/api/repositories.service';
import { Feature, NewFeatureBody, Repository, SpecFolderRow } from '../../core/models';

type Mode = 'link' | 'stub';

/**
 * Create-feature dialog. Two modes are exposed:
 *  - `link`: pick an existing OpenSpec folder under the repo
 *  - `stub`: type a kebab-case slug; the dashboard creates the folder
 *           with a minimal proposal.md
 *
 * `inline` mode is reserved for the PM agent (Phase 7) and is not exposed here.
 */
@Component({
  selector: 'app-new-feature-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatListModule,
    MatProgressSpinnerModule,
    MatSelectModule,
  ],
  templateUrl: './new-feature-dialog.html',
  styleUrl: './new-feature-dialog.scss',
})
export class NewFeatureDialog {
  private readonly featuresApi = inject(FeaturesService);
  private readonly reposApi = inject(RepositoriesService);

  readonly repositories = signal<Repository[]>([]);
  readonly repositoriesLoading = signal(true);
  readonly selectedRepoId = signal<string>('');
  readonly mode = signal<Mode>('link');

  readonly name = signal('');
  readonly description = signal('');
  readonly stubSlug = signal('');

  readonly specs = signal<SpecFolderRow[]>([]);
  readonly specsLoading = signal(false);
  readonly selectedSlug = signal<string>('');

  readonly errorMessage = signal<string | null>(null);
  readonly busy = signal(false);

  /** kebab-case (lower letters/digits, hyphens, no leading/trailing hyphen) */
  private readonly kebab = /^[a-z0-9][a-z0-9-]*[a-z0-9]$/;

  readonly selectedRepo = computed(() => {
    const id = this.selectedRepoId();
    return this.repositories().find((r) => r.id === id) ?? null;
  });

  readonly slugValid = computed(() => {
    const v = this.stubSlug().trim();
    return v.length > 0 && this.kebab.test(v);
  });

  readonly canSubmit = computed(() => {
    if (this.busy()) return false;
    if (!this.selectedRepoId() || !this.name().trim()) return false;
    const repo = this.selectedRepo();
    if (!repo || repo.isBroken) return false;
    if (this.mode() === 'link') return !!this.selectedSlug();
    return this.slugValid();
  });

  constructor(private readonly dialogRef: MatDialogRef<NewFeatureDialog, Feature | null>) {
    this.loadRepositories();
  }

  private loadRepositories(): void {
    this.repositoriesLoading.set(true);
    this.reposApi.list().subscribe({
      next: (list) => {
        this.repositories.set(list);
        this.repositoriesLoading.set(false);
      },
      error: () => {
        this.repositoriesLoading.set(false);
        this.errorMessage.set('Failed to load repositories.');
      },
    });
  }

  onRepoChanged(id: string): void {
    this.selectedRepoId.set(id);
    this.selectedSlug.set('');
    this.specs.set([]);
    this.errorMessage.set(null);
    if (this.mode() === 'link') this.loadSpecsIfNeeded();
  }

  onModeChanged(m: Mode): void {
    this.mode.set(m);
    this.errorMessage.set(null);
    if (m === 'link') this.loadSpecsIfNeeded();
  }

  private loadSpecsIfNeeded(): void {
    const id = this.selectedRepoId();
    if (!id) return;
    const repo = this.selectedRepo();
    if (!repo || repo.isBroken) return;
    this.specsLoading.set(true);
    this.reposApi.listSpecs(id).subscribe({
      next: (rows) => {
        this.specs.set(rows);
        this.specsLoading.set(false);
      },
      error: () => {
        this.specsLoading.set(false);
        this.errorMessage.set('Failed to load spec folders.');
      },
    });
  }

  selectSlug(slug: string, disabled: boolean): void {
    if (disabled) return;
    this.selectedSlug.set(slug);
  }

  cancel(): void {
    this.dialogRef.close(null);
  }

  submit(): void {
    if (!this.canSubmit()) return;
    this.errorMessage.set(null);
    const repoId = this.selectedRepoId();
    const name = this.name().trim();
    const description = this.description().trim() || null;

    const body: NewFeatureBody =
      this.mode() === 'link'
        ? { repositoryId: repoId, name, description, mode: 'link', specFolderSlug: this.selectedSlug() }
        : { repositoryId: repoId, name, description, mode: 'stub', specSlug: this.stubSlug().trim() };

    this.busy.set(true);
    this.featuresApi.create(body).subscribe({
      next: (created) => this.dialogRef.close(created),
      error: (err) => {
        this.busy.set(false);
        this.errorMessage.set(this.extractMessage(err));
      },
    });
  }

  private extractMessage(err: unknown): string {
    const e = err as { error?: { message?: string }; message?: string };
    return e?.error?.message ?? e?.message ?? 'Create failed.';
  }
}
