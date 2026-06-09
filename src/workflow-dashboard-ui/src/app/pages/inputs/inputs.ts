import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { InputRequestsService } from '../../core/api/input-requests.service';
import { InputRequest } from '../../core/models';
import { SignalRService } from '../../core/realtime/signalr.service';

interface PendingInput extends InputRequest {
  draft: string;
  parsedOptions: string[];
  submitting?: boolean;
}

@Component({
  selector: 'app-inputs',
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatChipsModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './inputs.html',
  styleUrl: './inputs.scss',
})
export class InputsPage {
  private readonly api = inject(InputRequestsService);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly snackBar = inject(MatSnackBar);

  readonly loading = signal(true);
  readonly items = signal<PendingInput[]>([]);
  readonly count = computed(() => this.items().length);

  constructor() {
    this.load();

    this.signalR.inputRequested$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((incoming) => {
        if (incoming.status !== 'pending') return;
        this.items.update((list) =>
          list.some((i) => i.id === incoming.id) ? list : [this.enrich(incoming), ...list],
        );
      });

    this.signalR.inputAnswered$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((answered) => {
        this.items.update((list) => list.filter((i) => i.id !== answered.id));
      });
  }

  load(): void {
    this.loading.set(true);
    this.api.list('pending').subscribe({
      next: (list) => {
        this.items.set(list.map((i) => this.enrich(i)));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private enrich(input: InputRequest): PendingInput {
    let parsedOptions: string[] = [];
    if (input.optionsJson) {
      try {
        const opts = JSON.parse(input.optionsJson);
        if (Array.isArray(opts)) parsedOptions = opts.map((o) => String(o));
      } catch {
        // ignore parse errors
      }
    }
    return { ...input, draft: '', parsedOptions };
  }

  updateDraft(id: string, value: string): void {
    this.items.update((list) => list.map((i) => (i.id === id ? { ...i, draft: value } : i)));
  }

  pickOption(id: string, value: string): void {
    this.updateDraft(id, value);
  }

  submit(item: PendingInput): void {
    const response = (item.draft || '').trim();
    if (!response) {
      this.snackBar.open('Please enter a response', 'Dismiss', { duration: 2000 });
      return;
    }
    this.items.update((list) => list.map((i) => (i.id === item.id ? { ...i, submitting: true } : i)));

    this.api.answer(item.id, { response }).subscribe({
      next: () => {
        this.items.update((list) => list.filter((i) => i.id !== item.id));
        this.snackBar.open('Response submitted', 'Dismiss', { duration: 1500 });
      },
      error: () => {
        this.items.update((list) =>
          list.map((i) => (i.id === item.id ? { ...i, submitting: false } : i)),
        );
        this.snackBar.open('Submit failed', 'Dismiss', { duration: 3000 });
      },
    });
  }
}
