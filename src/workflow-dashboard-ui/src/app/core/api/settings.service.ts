import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { AppSettingsDto } from '../models';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/settings';

  /** null = not yet checked, true/false = result of last check */
  readonly agentsDirConfigured = signal<boolean | null>(null);
  private _statusLoaded = false;

  get(): Observable<AppSettingsDto> {
    return this.http.get<AppSettingsDto>(this.base);
  }

  save(dto: AppSettingsDto): Observable<AppSettingsDto> {
    return this.http.put<AppSettingsDto>(this.base, dto);
  }

  reset(): Observable<void> {
    return this.http.delete<void>(this.base);
  }

  /** Fetches and caches the configured status. Safe to call from multiple pages — only one HTTP call is made. */
  loadStatusIfNeeded(): void {
    if (this._statusLoaded) return;
    this._statusLoaded = true;
    this.get().subscribe({
      next: (s) => this.agentsDirConfigured.set(!!s.agentsDir),
      error: () => { this._statusLoaded = false; },
    });
  }

  /** Call after saving or resetting settings to re-check status. */
  invalidateStatus(): void {
    this._statusLoaded = false;
    this.agentsDirConfigured.set(null);
    this.loadStatusIfNeeded();
  }
}
