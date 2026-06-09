import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { InputAnswer, InputRequest, InputRequestStatus } from '../models';

@Injectable({ providedIn: 'root' })
export class InputRequestsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/inputrequests';

  list(status?: InputRequestStatus): Observable<InputRequest[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    return this.http.get<InputRequest[]>(this.base, { params });
  }

  get(id: string): Observable<InputRequest> {
    return this.http.get<InputRequest>(`${this.base}/${id}`);
  }

  create(request: Partial<InputRequest>): Observable<InputRequest> {
    return this.http.post<InputRequest>(this.base, request);
  }

  answer(id: string, answer: InputAnswer): Observable<InputRequest> {
    return this.http.put<InputRequest>(`${this.base}/${id}`, answer);
  }
}
