import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RepositoriesService } from '../../core/api/repositories.service';
import { Repository } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';
import {
  RepositoryDialog,
  RepositoryDialogData,
} from './repository-dialog';

@Component({
  selector: 'app-repositories',
  imports: [
    CommonModule,
    DatePipe,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatDialogModule,
  ],
  templateUrl: './repositories.html',
  styleUrl: './repositories.scss',
})
export class RepositoriesPage {
  private readonly api = inject(RepositoriesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly signalR = inject(SignalRService);

  readonly columns = ['name', 'path', 'features', 'status', 'updatedAt', 'actions'];

  readonly repositories = signal<Repository[]>([]);
  readonly loading = signal(true);

  constructor() {
    this.load();

    // Live updates from SignalR (covers other browser tabs and PUT/POST/DELETE from anywhere).
    this.signalR.repositoryUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((r) => this.applyEvent(r));
  }

  private load(): void {
    this.loading.set(true);
    this.api
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.repositories.set(list);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.snackBar.open('Failed to load repositories', 'Dismiss', { duration: 3000 });
        },
      });
  }

  private applyEvent(repo: Repository): void {
    this.repositories.update((list) => {
      if (repo.deleted) {
        return list.filter((r) => r.id !== repo.id);
      }
      const idx = list.findIndex((r) => r.id === repo.id);
      if (idx === -1) return [repo, ...list];
      const next = list.slice();
      next[idx] = repo;
      return next;
    });
  }

  openAddDialog(): void {
    this.openDialog({ mode: 'add' });
  }

  openEditDialog(repo: Repository): void {
    this.openDialog({ mode: 'edit', repository: repo });
  }

  openRepairDialog(repo: Repository): void {
    this.openDialog({ mode: 'edit', repository: repo, highlightPath: true });
  }

  private openDialog(data: RepositoryDialogData): void {
    const ref = this.dialog.open<RepositoryDialog, RepositoryDialogData, Repository | null>(
      RepositoryDialog,
      { data, width: '520px', autoFocus: 'first-tabbable' },
    );
    ref.afterClosed().subscribe((saved) => {
      if (!saved) return;
      this.applyEvent(saved);
      this.snackBar.open(
        data.mode === 'add' ? 'Repository added' : 'Repository saved',
        'Dismiss',
        { duration: 2000 },
      );
    });
  }

  remove(repo: Repository): void {
    if (!confirm(`Remove repository "${repo.name}"?\n\n${repo.path}\n\nFeatures linked to this repository (if any) will become unlinked.`)) {
      return;
    }
    this.api.delete(repo.id).subscribe({
      next: () => {
        this.repositories.update((list) => list.filter((r) => r.id !== repo.id));
        this.snackBar.open('Repository removed', 'Dismiss', { duration: 2000 });
      },
      error: () => this.snackBar.open('Delete failed', 'Dismiss', { duration: 3000 }),
    });
  }
}
