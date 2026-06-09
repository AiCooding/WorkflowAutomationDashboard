import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DashboardSummary } from '../models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/dashboard';

  summary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.base}/summary`);
  }
}
