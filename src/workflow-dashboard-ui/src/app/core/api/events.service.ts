import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { EventType, WorkflowEvent } from '../models';

export interface EventFilter {
  workflowId?: string;
  agentId?: string;
  eventType?: EventType | string;
  limit?: number;
}

@Injectable({ providedIn: 'root' })
export class EventsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/events';

  list(filter?: EventFilter): Observable<WorkflowEvent[]> {
    let params = new HttpParams();
    if (filter?.workflowId) params = params.set('workflowId', filter.workflowId);
    if (filter?.agentId) params = params.set('agentId', filter.agentId);
    if (filter?.eventType) params = params.set('eventType', filter.eventType);
    if (filter?.limit) params = params.set('limit', filter.limit);
    return this.http.get<WorkflowEvent[]>(this.base, { params });
  }

  log(event: Partial<WorkflowEvent>): Observable<WorkflowEvent> {
    return this.http.post<WorkflowEvent>(this.base, event);
  }
}
