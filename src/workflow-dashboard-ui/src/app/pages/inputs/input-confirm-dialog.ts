import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';

export interface InputConfirmDialogData {
  question: string;
  response: string;
  decision: 'accepted' | 'rejected';
}

@Component({
  selector: 'app-input-confirm-dialog',
  standalone: true,
  imports: [MatButtonModule, MatCardModule, MatIconModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Confirm your response</h2>
    <mat-dialog-content>
      <p class="question-label"><strong>Question</strong></p>
      <p class="question-text">{{ data.question }}</p>
      <p class="response-label"><strong>Your answer</strong></p>
      <blockquote class="response-text">{{ data.response }}</blockquote>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="abort()">
        <mat-icon>close</mat-icon>
        Abort
      </button>
      <button mat-flat-button color="primary" (click)="confirm()">
        <mat-icon>check</mat-icon>
        Confirm
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .question-label, .response-label { margin: 0 0 4px; }
    .question-text { margin: 0 0 16px; opacity: 0.8; }
    blockquote.response-text {
      margin: 0 0 8px;
      padding: 10px 14px;
      border-left: 4px solid #1976d2;
      background: rgba(25,118,210,.06);
      border-radius: 0 4px 4px 0;
      white-space: pre-wrap;
      word-break: break-word;
    }
  `],
})
export class InputConfirmDialogComponent {
  readonly data = inject<InputConfirmDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<InputConfirmDialogComponent>);

  confirm(): void {
    this.dialogRef.close(true);
  }

  abort(): void {
    this.dialogRef.close(false);
  }
}
