import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Workflow, WorkflowStatus, WorkflowStatusUpdate } from '../models';

@Injectable({ providedIn: 'root' })
export class WorkflowsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/workflows';

  list(filter?: { status?: WorkflowStatus; featureId?: string }): Observable<Workflow[]> {
    let params = new HttpParams();
    if (filter?.status) params = params.set('status', filter.status);
    if (filter?.featureId) params = params.set('featureId', filter.featureId);
    return this.http.get<Workflow[]>(this.base, { params });
  }

  get(id: string): Observable<Workflow> {
    return this.http.get<Workflow>(`${this.base}/${id}`);
  }

  create(workflow: Partial<Workflow>): Observable<Workflow> {
    return this.http.post<Workflow>(this.base, workflow);
  }

  updateStatus(id: string, update: WorkflowStatusUpdate): Observable<Workflow> {
    return this.http.put<Workflow>(`${this.base}/${id}/status`, update);
  }
}
