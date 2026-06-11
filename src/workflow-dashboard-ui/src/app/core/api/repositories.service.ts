import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Repository, RepositoryCreate, RepositoryUpdate, SpecFolderRow } from '../models';

@Injectable({ providedIn: 'root' })
export class RepositoriesService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/repositories';

  list(): Observable<Repository[]> {
    return this.http.get<Repository[]>(this.base);
  }

  get(id: string): Observable<Repository> {
    return this.http.get<Repository>(`${this.base}/${id}`);
  }

  create(body: RepositoryCreate): Observable<Repository> {
    return this.http.post<Repository>(this.base, body);
  }

  update(id: string, body: RepositoryUpdate): Observable<Repository> {
    return this.http.put<Repository>(`${this.base}/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /** Spec-folder picker — lists `openspec/specs/*` under the repo root. */
  listSpecs(id: string): Observable<SpecFolderRow[]> {
    return this.http.get<SpecFolderRow[]>(`${this.base}/${id}/specs`);
  }

  /**
   * Lazy fetch of a single spec file. <c>filename</c> must be one of
   * `proposal.md` | `design.md` | `tasks.md`. Returns raw markdown text.
   */
  getSpecFile(id: string, slug: string, filename: string): Observable<string> {
    return this.http.get(`${this.base}/${id}/specs/${slug}/files/${filename}`, {
      responseType: 'text',
    });
  }
}

