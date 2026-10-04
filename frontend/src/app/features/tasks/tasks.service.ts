import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../../core/api.config';
import { TaskItem, TaskPayload, TaskStatus } from '../../core/models/task.model';

/** Thin HTTP wrapper: one method per backend endpoint, no UI logic. */
@Injectable({ providedIn: 'root' })
export class TasksService {
  private readonly http = inject(HttpClient);
  private readonly url = `${API_BASE_URL}/tasks`;

  list(): Observable<TaskItem[]> {
    return this.http.get<TaskItem[]>(this.url);
  }

  create(payload: TaskPayload): Observable<TaskItem> {
    return this.http.post<TaskItem>(this.url, payload);
  }

  update(id: number, payload: TaskPayload): Observable<TaskItem> {
    return this.http.put<TaskItem>(`${this.url}/${id}`, payload);
  }

  setStatus(id: number, status: TaskStatus): Observable<TaskItem> {
    return this.http.patch<TaskItem>(`${this.url}/${id}/status`, { status });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
