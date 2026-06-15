import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CatalogService } from '../../core/api/catalog.service';
import { SettingsService } from '../../core/api/settings.service';
import { CatalogEntry } from '../../core/models';

@Component({
  selector: 'app-agents',
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './agents.html',
  styleUrl: './agents.scss',
})
export class AgentsPage {
  private readonly catalog = inject(CatalogService);
  protected readonly settings = inject(SettingsService);

  readonly loading = signal(true);
  readonly all = signal<CatalogEntry[]>([]);

  readonly agents = computed(() => this.all().filter((e) => e.kind === 'agent'));
  readonly brokenCount = computed(() => this.agents().filter((e) => e.isBroken).length);

  constructor() {
    this.settings.loadStatusIfNeeded();
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.catalog.list().subscribe({
      next: (list) => {
        this.all.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
