import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CatalogEntry, CatalogEntryDetail, CatalogRefreshResult } from '../models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/catalog';

  list(): Observable<CatalogEntry[]> {
    return this.http.get<CatalogEntry[]>(this.base);
  }

  refresh(): Observable<CatalogRefreshResult> {
    return this.http.post<CatalogRefreshResult>(`${this.base}/refresh`, {});
  }

  get(kind: string, slug: string): Observable<CatalogEntryDetail> {
    return this.http.get<CatalogEntryDetail>(`${this.base}/${kind}/${encodeURIComponent(slug)}`);
  }

  sourceUrl(kind: string, slug: string): string {
    return `${this.base}/${kind}/${encodeURIComponent(slug)}/source`;
  }
}
