import { CommonModule } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { CatalogService } from '../../core/api/catalog.service';
import { CatalogEntry } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';

@Component({
  selector: 'app-catalog',
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatListModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './catalog.html',
  styleUrl: './catalog.scss',
})
export class CatalogPage {
  private readonly api = inject(CatalogService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);

  readonly entries = signal<CatalogEntry[]>([]);
  readonly loading = signal(true);
  readonly refreshing = signal(false);

  readonly workflows = computed(() =>
    this.entries()
      .filter((e) => e.kind === 'workflow')
      .sort((a, b) => a.displayName.localeCompare(b.displayName)),
  );
  readonly agents = computed(() =>
    this.entries()
      .filter((e) => e.kind === 'agent')
      .sort((a, b) => a.displayName.localeCompare(b.displayName)),
  );
  readonly brokenCount = computed(() => this.entries().filter((e) => e.isBroken).length);

  constructor() {
    this.load();

    // Pick up refreshes triggered from other tabs / instances.
    this.signalR.catalogRefreshed$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load());
  }

  private load(): void {
    this.loading.set(true);
    this.api.list().subscribe({
      next: (list) => {
        this.entries.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load catalog', 'Dismiss', { duration: 3000 });
      },
    });
  }

  refresh(): void {
    if (this.refreshing()) return;
    this.refreshing.set(true);
    this.api.refresh().subscribe({
      next: (r) => {
        this.refreshing.set(false);
        this.snackBar.open(
          `${r.workflowCount} workflows, ${r.agentCount} agents, ${r.brokenCount} broken`,
          'Dismiss',
          { duration: 3000 },
        );
        // SignalR broadcast triggers reload, but reload anyway in case the
        // event arrives later than the HTTP response.
        this.load();
      },
      error: () => {
        this.refreshing.set(false);
        this.snackBar.open('Refresh failed', 'Dismiss', { duration: 3000 });
      },
    });
  }
}
