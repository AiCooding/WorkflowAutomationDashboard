import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Command, CommandProcessed, CommandStatus } from '../models';

@Injectable({ providedIn: 'root' })
export class CommandsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/commands';

  list(status?: CommandStatus): Observable<Command[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    return this.http.get<Command[]>(this.base, { params });
  }

  create(command: Partial<Command>): Observable<Command> {
    return this.http.post<Command>(this.base, command);
  }

  markProcessed(id: string, processed: CommandProcessed): Observable<Command> {
    return this.http.put<Command>(`${this.base}/${id}`, processed);
  }
}
