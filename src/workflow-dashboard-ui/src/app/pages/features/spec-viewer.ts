import { CommonModule } from '@angular/common';
import { Component, DestroyRef, Input, OnChanges, SimpleChanges, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { FeaturesService } from '../../core/api/features.service';
import { RepositoriesService } from '../../core/api/repositories.service';
import { SpecManifest } from '../../core/models';

type SpecTab = 'proposal' | 'design' | 'tasks';

interface TabState {
  loaded: boolean;
  loading: boolean;
  error?: string;
  /** Raw markdown content (set when loaded and `exists`). */
  content?: string;
  /** Server-rendered HTML — Phase 3 keeps it client-rendered as a <pre>
   *  because the lazy endpoint returns raw markdown, not HTML. The
   *  MarkdownRendererComponent expects HTML, so we wrap in <pre> for now. */
  html?: string;
}

const PERSONA_FOR: Record<SpecTab, string | null> = {
  proposal: null,
  design: 'architect',
  tasks: 'developer',
};

const FILENAME_FOR: Record<SpecTab, string> = {
  proposal: 'proposal.md',
  design: 'design.md',
  tasks: 'tasks.md',
};

/**
 * Tabbed Proposal / Design / Tasks viewer. Loads the 3-file manifest from
 * `GET /api/features/{id}/spec`, then lazily fetches each file's raw markdown
 * (via the repository per-file endpoint) on tab click. Empty tabs show a
 * greyed-out placeholder per the architect plan §5.
 */
@Component({
  selector: 'app-spec-viewer',
  standalone: true,
  imports: [CommonModule, MatTabsModule, MatProgressSpinnerModule],
  templateUrl: './spec-viewer.html',
  styleUrl: './spec-viewer.scss',
})
export class SpecViewer implements OnChanges {
  private readonly featuresApi = inject(FeaturesService);
  private readonly reposApi = inject(RepositoriesService);
  private readonly destroyRef = inject(DestroyRef);

  @Input({ required: true }) featureId!: string;

  readonly manifest = signal<SpecManifest | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly tabs: SpecTab[] = ['proposal', 'design', 'tasks'];
  readonly state = signal<Record<SpecTab, TabState>>(this.emptyState());

  ngOnChanges(_: SimpleChanges): void {
    this.load();
  }

  private emptyState(): Record<SpecTab, TabState> {
    return {
      proposal: { loaded: false, loading: false },
      design: { loaded: false, loading: false },
      tasks: { loaded: false, loading: false },
    };
  }

  personaFor(tab: SpecTab): string | null {
    return PERSONA_FOR[tab];
  }

  emptyMessage(tab: SpecTab): string {
    const filename = FILENAME_FOR[tab];
    const persona = PERSONA_FOR[tab];
    if (persona) {
      return `${filename} not yet written. The ${persona} agent will produce this on its first run.`;
    }
    return `${filename} not yet written. Use the PM workflow to draft this file.`;
  }

  exists(tab: SpecTab): boolean {
    const m = this.manifest();
    if (!m) return false;
    return tab === 'proposal' ? m.proposal.exists : tab === 'design' ? m.design.exists : m.tasks.exists;
  }

  private load(): void {
    if (!this.featureId) return;
    this.loading.set(true);
    this.error.set(null);
    this.manifest.set(null);
    this.state.set(this.emptyState());

    this.featuresApi
      .getSpec(this.featureId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (m) => {
          this.manifest.set(m);
          this.loading.set(false);
          // Eagerly load proposal since it's the default tab.
          if (m.proposal.exists) {
            this.applyContent('proposal', m.proposal.content ?? '');
          }
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(this.extractMessage(err));
        },
      });
  }

  /** Called when a tab becomes active (mat-tab `selectedTabChange`). */
  onTabSelected(index: number): void {
    const tab = this.tabs[index];
    if (!tab) return;
    this.ensureLoaded(tab);
  }

  private ensureLoaded(tab: SpecTab): void {
    const m = this.manifest();
    if (!m) return;
    const current = this.state()[tab];
    if (current.loaded || current.loading) return;
    if (!this.exists(tab)) return;

    // proposal is preloaded from the manifest; design/tasks lazy-load via the
    // per-file endpoint as the plan dictates.
    if (tab === 'proposal') {
      this.applyContent('proposal', m.proposal.content ?? '');
      return;
    }

    this.updateState(tab, { loading: true });
    this.reposApi
      .getSpecFile(m.repositoryId, this.slugFromFolder(m.folder), FILENAME_FOR[tab])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (content) => this.applyContent(tab, content),
        error: (err) => this.updateState(tab, { loading: false, loaded: true, error: this.extractMessage(err) }),
      });
  }

  private applyContent(tab: SpecTab, content: string): void {
    this.updateState(tab, { loading: false, loaded: true, content });
  }

  private updateState(tab: SpecTab, patch: Partial<TabState>): void {
    this.state.update((s) => ({ ...s, [tab]: { ...s[tab], ...patch } }));
  }

  private slugFromFolder(folder: string): string {
    // folder is `openspec/specs/{slug}` → last segment.
    const parts = folder.split('/').filter(Boolean);
    return parts[parts.length - 1] ?? folder;
  }

  private extractMessage(err: unknown): string {
    const e = err as { error?: { message?: string }; message?: string; status?: number };
    if (e?.error?.message) return e.error.message;
    if (e?.message) return e.message;
    return 'Failed to load.';
  }
}
