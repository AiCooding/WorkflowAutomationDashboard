import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CatalogService } from '../../core/api/catalog.service';
import { MarkdownRendererComponent } from '../../core/markdown/markdown-renderer.component';
import { CatalogEntryDetail } from '../../core/models';
import { LaunchWorkflowDialog } from './launch-workflow-dialog';

@Component({
  selector: 'app-catalog-detail',
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MarkdownRendererComponent,
  ],
  templateUrl: './catalog-detail.html',
  styleUrl: './catalog-detail.scss',
})
export class CatalogDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(CatalogService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  readonly detail = signal<CatalogEntryDetail | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((p) => {
      const kind = p.get('kind') ?? '';
      const slug = p.get('slug') ?? '';
      this.load(kind, slug);
    });
  }

  launchable(): boolean {
    return false;
  }

  openLaunch(): void {
    const d = this.detail();
    if (!d) return;
    const ref = this.dialog.open(LaunchWorkflowDialog, {
      data: { catalogSlug: d.entry.slug, displayName: d.entry.displayName },
    });
    ref.afterClosed().subscribe((result) => {
      if (result) this.router.navigate(['/pipelines']);
    });
  }

  private load(kind: string, slug: string): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.api.get(kind, slug).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err?.status === 404) {
          this.notFound.set(true);
        } else {
          this.snackBar.open('Failed to load entry', 'Dismiss', { duration: 3000 });
        }
      },
    });
  }
}
