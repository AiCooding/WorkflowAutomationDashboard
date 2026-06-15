import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApprovalDecisionBody, PipelineRun, RestartRunBody, StartPipelineRunBody } from '../models';

@Injectable({ providedIn: 'root' })
export class PipelineRunsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/pipeline-runs';

  list(filters?: { repositoryId?: string; featureId?: string; pipelineId?: string; status?: string }): Observable<PipelineRun[]> {
    let params = new HttpParams();
    if (filters?.repositoryId) params = params.set('repositoryId', filters.repositoryId);
    if (filters?.featureId) params = params.set('featureId', filters.featureId);
    if (filters?.pipelineId) params = params.set('pipelineId', filters.pipelineId);
    if (filters?.status) params = params.set('status', filters.status);
    return this.http.get<PipelineRun[]>(this.base, { params });
  }

  get(runId: string): Observable<PipelineRun> {
    return this.http.get<PipelineRun>(`${this.base}/${runId}`);
  }

  start(body: StartPipelineRunBody): Observable<PipelineRun> {
    return this.http.post<PipelineRun>(this.base, body);
  }

  cancel(runId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${runId}/cancel`, {});
  }

  restart(runId: string, body: RestartRunBody): Observable<PipelineRun> {
    return this.http.post<PipelineRun>(`${this.base}/${runId}/restart`, body);
  }

  decide(runId: string, approvalId: string, body: ApprovalDecisionBody): Observable<void> {
    return this.http.post<void>(`${this.base}/${runId}/approvals/${approvalId}/decide`, body);
  }
}
