import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AgentsService } from '../../core/api/agents.service';
import { Agent, AgentStatus } from '../../core/models';
import { formatDuration, StatusStyle } from '../../shared/status-style';
import { SignalRService } from '../../core/realtime/signalr.service';

const STATUSES: AgentStatus[] = ['idle', 'running', 'waiting_input', 'completed', 'failed'];

@Component({
  selector: 'app-agents',
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    MatTableModule,
    MatCardModule,
    MatFormFieldModule,
    MatSelectModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './agents.html',
  styleUrl: './agents.scss',
})
export class AgentsPage {
  private readonly agentsApi = inject(AgentsService);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);
  readonly styles = inject(StatusStyle);

  readonly statuses = STATUSES;
  readonly statusFilter = signal<AgentStatus | ''>('');
  readonly loading = signal(true);
  readonly agents = signal<Agent[]>([]);

  readonly columns = ['agentType', 'status', 'currentTask', 'duration', 'sessionId'];

  constructor() {
    this.load();

    this.signalR.agentUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((updated) => {
        this.agents.update((list) => {
          const i = list.findIndex((a) => a.id === updated.id);
          if (i === -1) return [updated, ...list];
          const copy = [...list];
          copy[i] = updated;
          return copy;
        });
      });
  }

  load(): void {
    this.loading.set(true);
    const status = this.statusFilter() || undefined;
    this.agentsApi.list(status).subscribe({
      next: (list) => {
        this.agents.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onStatusChange(s: AgentStatus | ''): void {
    this.statusFilter.set(s);
    this.load();
  }

  duration(a: Agent): string {
    return formatDuration(a.startedAt, a.completedAt);
  }
}
