import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { DashboardService } from '../../core/api/dashboard.service';
import { DashboardSummary } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';

@Component({
  selector: 'app-control',
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
  ],
  templateUrl: './control.html',
  styleUrl: './control.scss',
})
export class ControlPage {
  private readonly dashboard = inject(DashboardService);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);

  readonly summary = signal<DashboardSummary | null>(null);

  constructor() {
    this.refresh();

    this.signalR.pipelineRunUpdated$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refresh());
    this.signalR.approvalRequested$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refresh());
    this.signalR.approvalDecided$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refresh());
  }

  private refresh(): void {
    this.dashboard.summary().subscribe({
      next: (summary) => this.summary.set(summary),
      error: () => this.summary.set(null),
    });
  }
}
