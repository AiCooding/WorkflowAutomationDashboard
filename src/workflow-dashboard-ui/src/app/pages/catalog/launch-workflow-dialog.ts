import { CommonModule } from '@angular/common';
import { Component, Inject, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RepositoriesService } from '../../core/api/repositories.service';
import { RunsService } from '../../core/api/runs.service';
import { Repository } from '../../core/models';

export interface LaunchDialogData {
  catalogSlug: string;
  displayName: string;
}

@Component({
  selector: 'app-launch-workflow-dialog',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Launch workflow: {{ data.displayName }}</h2>
    <mat-dialog-content class="content">
      @if (loading()) {
        <div class="loading-row"><mat-spinner diameter="32"></mat-spinner></div>
      } @else {
        @if (repositories().length === 0) {
          <p class="empty">No repositories registered. Add one on the Repositories page first.</p>
        } @else {
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Repository</mat-label>
            <mat-select [(ngModel)]="repositoryId">
              @for (r of repositories(); track r.id) {
                <mat-option [value]="r.id" [disabled]="r.isBroken">
                  {{ r.name }} <span class="path-hint">— {{ r.path }}</span>
                  @if (r.isBroken) { <span class="broken-tag">(broken)</span> }
                </mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Feature ID (optional)</mat-label>
            <input matInput [(ngModel)]="featureId" placeholder="leave blank for PM-draft" />
          </mat-form-field>

          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Payload JSON (optional)</mat-label>
            <textarea matInput rows="4" [(ngModel)]="payloadText" placeholder='{"key": "value"}'></textarea>
          </mat-form-field>

          @if (error()) { <div class="error">{{ error() }}</div> }
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="launching()">Cancel</button>
      <button mat-raised-button color="primary"
              [disabled]="!repositoryId || launching() || repositories().length === 0"
              (click)="launch()">
        @if (launching()) { <mat-spinner diameter="18"></mat-spinner> }
        <span style="margin-left: 6px;">Launch</span>
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .content { display: flex; flex-direction: column; gap: 16px; min-width: 420px; padding-top: 12px; }
    .loading-row { display: flex; justify-content: center; padding: 32px; }
    .empty { opacity: 0.7; }
    .path-hint { opacity: 0.6; font-size: 12px; }
    .broken-tag { color: #dc2626; margin-left: 4px; font-size: 12px; }
    .error { color: #dc2626; font-size: 13px; }
  `],
})
export class LaunchWorkflowDialog implements OnInit {
  private readonly repositoriesApi = inject(RepositoriesService);
  private readonly runsApi = inject(RunsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialogRef = inject(MatDialogRef<LaunchWorkflowDialog>);

  readonly loading = signal(true);
  readonly launching = signal(false);
  readonly repositories = signal<Repository[]>([]);
  readonly error = signal<string | null>(null);

  repositoryId = '';
  featureId = '';
  payloadText = '';

  constructor(@Inject(MAT_DIALOG_DATA) public data: LaunchDialogData) {}

  ngOnInit(): void {
    this.repositoriesApi.list().subscribe({
      next: (list) => {
        this.repositories.set(list);
        const first = list.find((r) => !r.isBroken);
        if (first) this.repositoryId = first.id;
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  launch(): void {
    this.error.set(null);
    let payload: unknown = undefined;
    if (this.payloadText.trim()) {
      try { payload = JSON.parse(this.payloadText); }
      catch (e: any) { this.error.set('Payload must be valid JSON.'); return; }
    }
    this.launching.set(true);
    this.runsApi.launch({
      catalogSlug: this.data.catalogSlug,
      repositoryId: this.repositoryId,
      featureId: this.featureId.trim() || null,
      payload,
    }).subscribe({
      next: (w) => {
        this.launching.set(false);
        this.snackBar.open(`Launched ${w.id}`, 'Dismiss', { duration: 2500 });
        this.dialogRef.close(w);
      },
      error: (err) => {
        this.launching.set(false);
        const msg = err?.error?.message ?? err.message ?? 'Launch failed';
        this.error.set(msg);
      },
    });
  }
}
