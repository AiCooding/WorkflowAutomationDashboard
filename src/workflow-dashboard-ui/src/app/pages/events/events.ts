import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { EventsService } from '../../core/api/events.service';
import { EventType, WorkflowEvent } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';
import { StatusStyle } from '../../shared/status-style';

const TYPES: EventType[] = [
  'state_change', 'log', 'error', 'input_requested',
];

@Component({
  selector: 'app-events',
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './events.html',
  styleUrl: './events.scss',
})
export class EventsPage {
  private readonly api = inject(EventsService);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);
  readonly styles = inject(StatusStyle);

  readonly types = TYPES;
  readonly loading = signal(true);
  readonly all = signal<WorkflowEvent[]>([]);

  readonly workflowFilter = signal('');
  readonly agentFilter = signal('');
  readonly typeFilter = signal<EventType | ''>('');
  readonly searchText = signal('');
  readonly limit = signal(50);

  readonly filtered = computed(() => {
    const wf = this.workflowFilter().trim().toLowerCase();
    const ag = this.agentFilter().trim().toLowerCase();
    const t = this.typeFilter();
    const search = this.searchText().trim().toLowerCase();

    return this.all().filter((e) => {
      if (wf && !(e.workflowId || '').toLowerCase().includes(wf)) return false;
      if (ag && !(e.agentId || '').toLowerCase().includes(ag)) return false;
      if (t && e.eventType !== t) return false;
      if (search && !(e.message || '').toLowerCase().includes(search)) return false;
      return true;
    });
  });

  constructor() {
    this.load();

    this.signalR.eventLogged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((evt) => {
        this.all.update((list) => [evt, ...list].slice(0, this.limit()));
      });
  }

  load(): void {
    this.loading.set(true);
    this.api
      .list({
        workflowId: this.workflowFilter().trim() || undefined,
        agentId: this.agentFilter().trim() || undefined,
        eventType: this.typeFilter() || undefined,
        limit: this.limit(),
      })
      .subscribe({
        next: (list) => {
          this.all.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  clearFilters(): void {
    this.workflowFilter.set('');
    this.agentFilter.set('');
    this.typeFilter.set('');
    this.searchText.set('');
    this.load();
  }
}
