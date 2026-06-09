import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Feature, FeatureSpec, FeatureSpecUpdate, FeatureStatus } from '../models';

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

  create(feature: Partial<Feature>): Observable<Feature> {
    return this.http.post<Feature>(this.base, feature);
  }

  update(id: string, feature: Partial<Feature>): Observable<Feature> {
    return this.http.put<Feature>(`${this.base}/${id}`, feature);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  getSpec(id: string): Observable<FeatureSpec> {
    return this.http.get<FeatureSpec>(`${this.base}/${id}/spec`);
  }

  saveSpec(id: string, body: FeatureSpecUpdate): Observable<FeatureSpec> {
    return this.http.post<FeatureSpec>(`${this.base}/${id}/spec`, body);
  }
}
