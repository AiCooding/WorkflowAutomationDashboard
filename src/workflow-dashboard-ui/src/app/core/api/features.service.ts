import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Feature, FeatureStatus, NewFeatureBody, SpecManifest } from '../models';

@Injectable({ providedIn: 'root' })
export class FeaturesService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/features';

  list(status?: FeatureStatus): Observable<Feature[]> {
    let params = new HttpParams();
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<Feature[]>(this.base, { params });
  }

  get(id: string): Observable<Feature> {
    return this.http.get<Feature>(`${this.base}/${id}`);
  }

  /** Polymorphic create — mode is one of 'link' | 'stub' | 'inline'. */
  create(body: NewFeatureBody, workflowId?: string): Observable<Feature> {
    let params = new HttpParams();
    if (workflowId) params = params.set('workflowId', workflowId);
    return this.http.post<Feature>(this.base, body, { params });
  }

  update(id: string, body: Partial<Feature>): Observable<Feature> {
    return this.http.put<Feature>(`${this.base}/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /** Returns the 3-file spec manifest. */
  getSpec(id: string): Observable<SpecManifest> {
    return this.http.get<SpecManifest>(`${this.base}/${id}/spec`);
  }
}
