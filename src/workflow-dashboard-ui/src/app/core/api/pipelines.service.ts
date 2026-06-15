import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Pipeline, PipelineExportDto } from '../models';

@Injectable({ providedIn: 'root' })
export class PipelinesService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/pipelines';

  list(): Observable<Pipeline[]> {
    return this.http.get<Pipeline[]>(this.base);
  }

  get(id: string): Observable<Pipeline> {
    return this.http.get<Pipeline>(`${this.base}/${id}`);
  }

  create(body: { name: string; description?: string | null; stepsJson?: string }): Observable<Pipeline> {
    return this.http.post<Pipeline>(this.base, body);
  }

  update(id: string, body: { name: string; description?: string | null; stepsJson?: string }): Observable<Pipeline> {
    return this.http.put<Pipeline>(`${this.base}/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  exportPipeline(id: string): Observable<PipelineExportDto> {
    return this.http.get<PipelineExportDto>(`${this.base}/${id}/export`);
  }

  importPipeline(dto: PipelineExportDto): Observable<Pipeline> {
    return this.http.post<Pipeline>(`${this.base}/import`, dto);
  }
}
