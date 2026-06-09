import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Agent, AgentStatus, AgentUpdate } from '../models';

@Injectable({ providedIn: 'root' })
export class AgentsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/agents';

  list(status?: AgentStatus): Observable<Agent[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    return this.http.get<Agent[]>(this.base, { params });
  }

  get(id: string): Observable<Agent> {
    return this.http.get<Agent>(`${this.base}/${id}`);
  }

  create(agent: Partial<Agent>): Observable<Agent> {
    return this.http.post<Agent>(this.base, agent);
  }

  update(id: string, update: AgentUpdate): Observable<Agent> {
    return this.http.put<Agent>(`${this.base}/${id}`, update);
  }
}
