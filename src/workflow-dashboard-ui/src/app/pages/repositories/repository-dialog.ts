import { CommonModule } from '@angular/common';
import { Component, Inject, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RepositoriesService } from '../../core/api/repositories.service';
import { Repository, RepositoryCreate, RepositoryUpdate } from '../../core/models';

export type RepositoryDialogMode = 'add' | 'edit';

export interface RepositoryDialogData {
  mode: RepositoryDialogMode;
  /** Required in edit mode; ignored in add mode. */
  repository?: Repository;
  /** Show the broken-repo banner and focus the path field. */
  highlightPath?: boolean;
}

/**
 * Dialog returns the saved Repository on success, or null if the user cancelled.
 * The dialog itself performs the HTTP call so server-side validation errors
 * (e.g. "path does not exist") can be displayed inline without re-opening.
 */
@Component({
  selector: 'app-repository-dialog',
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './repository-dialog.html',
  styleUrl: './repository-dialog.scss',
})
export class RepositoryDialog {
  private readonly api = inject(RepositoriesService);

  readonly mode: RepositoryDialogMode;
  readonly highlightPath: boolean;
  readonly originalRepo?: Repository;

  readonly name = signal('');
  readonly path = signal('');
  readonly errorMessage = signal<string | null>(null);
  readonly busy = signal(false);

  constructor(
    @Inject(MAT_DIALOG_DATA) data: RepositoryDialogData,
    private readonly dialogRef: MatDialogRef<RepositoryDialog, Repository | null>,
  ) {
    this.mode = data.mode;
    this.highlightPath = data.highlightPath === true;
    this.originalRepo = data.repository;

    if (this.mode === 'edit' && data.repository) {
      this.name.set(data.repository.name);
      this.path.set(data.repository.path);
    }
  }

  get title(): string {
    if (this.mode === 'add') return 'Add repository';
    return this.highlightPath ? 'Repair repository path' : 'Edit repository';
  }

  get submitLabel(): string {
    if (this.mode === 'add') return 'Add';
    return this.highlightPath ? 'Repair' : 'Save';
  }

  cancel(): void {
    this.dialogRef.close(null);
  }

  submit(): void {
    const name = this.name().trim();
    const path = this.path().trim();
    this.errorMessage.set(null);

    if (this.mode === 'add') {
      if (!path) {
        this.errorMessage.set('Path is required.');
        return;
      }
      const body: RepositoryCreate = name ? { path, name } : { path };
      this.busy.set(true);
      this.api.create(body).subscribe({
        next: (repo) => this.dialogRef.close(repo),
        error: (err) => this.handleError(err),
      });
      return;
    }

    // edit
    if (!this.originalRepo) {
      this.errorMessage.set('No repository to edit.');
      return;
    }
    const body: RepositoryUpdate = {};
    if (name && name !== this.originalRepo.name) body.name = name;
    if (path && path !== this.originalRepo.path) body.path = path;
    if (Object.keys(body).length === 0) {
      this.dialogRef.close(null);
      return;
    }
    this.busy.set(true);
    this.api.update(this.originalRepo.id, body).subscribe({
      next: (repo) => this.dialogRef.close(repo),
      error: (err) => this.handleError(err),
    });
  }

  private handleError(err: unknown): void {
    this.busy.set(false);
    const msg = this.extractMessage(err);
    this.errorMessage.set(msg);
  }

  private extractMessage(err: unknown): string {
    const e = err as { error?: { message?: string }; message?: string; status?: number };
    if (e?.error?.message) return e.error.message;
    if (e?.message) return e.message;
    if (e?.status === 400) return 'Bad request.';
    return 'Save failed.';
  }
}
