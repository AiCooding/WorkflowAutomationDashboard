import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { LaunchWorkflowBody, Workflow } from '../models';

@Injectable({ providedIn: 'root' })
export class RunsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/workflows';

  launch(body: LaunchWorkflowBody): Observable<Workflow> {
    return this.http.post<Workflow>(`${this.base}/launch`, body);
  }

  cancel(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/cancel`, {});
  }

  requeue(id: string): Observable<Workflow> {
    return this.http.post<Workflow>(`${this.base}/${id}/requeue`, {});
  }

  cancelAll(): Observable<{ cancelled: number }> {
    return this.http.post<{ cancelled: number }>(`${this.base}/cancel-all`, {});
  }
}
