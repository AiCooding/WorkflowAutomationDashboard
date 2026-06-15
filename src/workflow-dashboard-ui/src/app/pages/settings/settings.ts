import { Component, DestroyRef, inject, signal, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AppSettingsDto, AgentRunnerSettingsDto, CliTool } from '../../core/models';
import { SettingsService } from '../../core/api/settings.service';

interface ToolPreset {
  cliTool: CliTool;
  executable: string;
  instructionsRelativePath: string;
  inputFileRelativePath: string;
  interactiveStartPrompt: string;
}

const TOOL_PRESETS: Record<CliTool, ToolPreset> = {
  Copilot: {
    cliTool: 'Copilot',
    executable: 'copilot',
    instructionsRelativePath: '.github/instructions/active-workflow.instructions.md',
    inputFileRelativePath: '.github/copilot/workflow-input.md',
    interactiveStartPrompt:
      'Begin the workflow session. Read `.github/copilot/workflow-input.md` and follow the workflow instructions you have been given.',
  },
  Claude: {
    cliTool: 'Claude',
    executable: 'claude',
    instructionsRelativePath: 'CLAUDE.md',
    inputFileRelativePath: '.claude/workflow-input.md',
    interactiveStartPrompt:
      'Begin the workflow session. Read `.claude/workflow-input.md` and follow the workflow instructions you have been given.',
  },
  Custom: {
    cliTool: 'Custom',
    executable: '',
    instructionsRelativePath: '',
    inputFileRelativePath: '',
    interactiveStartPrompt: '',
  },
};

@Component({
  selector: 'app-settings',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule,
  ],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class SettingsPage implements OnInit {
  private readonly api = inject(SettingsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  readonly tools: CliTool[] = ['Copilot', 'Claude', 'Custom'];
  readonly loading = signal(true);
  readonly saving = signal(false);

  settings = signal<AppSettingsDto>({
    cliTool: 'Copilot',
    executable: 'copilot',
    extraArgs: [],
    instructionsRelativePath: '.github/instructions/active-workflow.instructions.md',
    inputFileRelativePath: '.github/copilot/workflow-input.md',
    interactiveTerminal: true,
    interactiveStartPrompt:
      'Begin the workflow session. Read `.github/copilot/workflow-input.md` and follow the workflow instructions you have been given.',
    enabled: true,
    agentsDir: null,
  });

  // Working copy for the form (mutable fields bound via ngModel)
  form: AppSettingsDto = { ...this.settings() };

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api
      .get()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (s) => {
          this.settings.set(s);
          this.form = { ...s, extraArgs: [...(s.extraArgs ?? [])] };
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.snackBar.open('Failed to load settings', 'Dismiss', { duration: 3000 });
        },
      });
  }

  onToolChange(tool: CliTool): void {
    if (tool === 'Custom') return; // Don't overwrite custom fields
    const preset = TOOL_PRESETS[tool];
    this.form = {
      ...this.form,
      ...preset,
    };
  }

  get extraArgsText(): string {
    return (this.form.extraArgs ?? []).join('\n');
  }

  set extraArgsText(value: string) {
    this.form.extraArgs = value
      .split('\n')
      .map((s) => s.trim())
      .filter((s) => s.length > 0);
  }

  save(): void {
    this.saving.set(true);
    this.api
      .save(this.form)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (saved) => {
          this.settings.set(saved);
          this.form = { ...saved, extraArgs: [...(saved.extraArgs ?? [])] };
          this.saving.set(false);
          this.api.invalidateStatus();
          this.snackBar.open('Settings saved', 'Dismiss', { duration: 2000 });
        },
        error: () => {
          this.saving.set(false);
          this.snackBar.open('Failed to save settings', 'Dismiss', { duration: 3000 });
        },
      });
  }

  reset(): void {
    if (!confirm('Reset to appsettings.json defaults?')) return;
    this.saving.set(true);
    this.api
      .reset()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.api.invalidateStatus();
          this.load();
          this.snackBar.open('Reset to defaults', 'Dismiss', { duration: 2000 });
        },
        error: () => {
          this.saving.set(false);
          this.snackBar.open('Reset failed', 'Dismiss', { duration: 3000 });
        },
      });
  }
}
