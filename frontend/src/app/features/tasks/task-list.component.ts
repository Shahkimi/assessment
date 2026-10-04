import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { toFriendlyError, toFriendlyErrors } from '../../core/http/api-error';
import { TaskItem, TaskPayload, TaskStatus } from '../../core/models/task.model';
import { TaskFormComponent } from './task-form.component';
import { TasksService } from './tasks.service';

/** `null` = form hidden, 'new' = creating, TaskItem = editing that task. */
type FormState = null | 'new' | TaskItem;

@Component({
  selector: 'app-task-list',
  standalone: true,
  imports: [DatePipe, TaskFormComponent],
  templateUrl: './task-list.component.html',
  styleUrl: './task-list.component.scss'
})
export class TaskListComponent implements OnInit {
  private readonly tasksService = inject(TasksService);
  private readonly auth = inject(AuthService);

  readonly tasks = signal<TaskItem[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly formState = signal<FormState>(null);
  readonly saving = signal(false);
  readonly formErrors = signal<string[]>([]);

  /** Row-level actions (done/undo/delete) in flight, so each row can disable itself. */
  readonly busyIds = signal<ReadonlySet<number>>(new Set());
  readonly actionError = signal<string | null>(null);

  readonly editingTask = computed(() => {
    const state = this.formState();
    return state && state !== 'new' ? state : null;
  });

  readonly openCount = computed(() => this.tasks().filter((t) => t.status === 'Todo').length);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.tasksService.list().subscribe({
      next: (tasks) => {
        this.tasks.set(tasks);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loadError.set(toFriendlyError(error));
        this.loading.set(false);
      }
    });
  }

  // ---- form ----

  openCreate(): void {
    this.formErrors.set([]);
    this.formState.set('new');
  }

  openEdit(task: TaskItem): void {
    this.formErrors.set([]);
    this.formState.set(task);
  }

  closeForm(): void {
    this.formState.set(null);
    this.formErrors.set([]);
  }

  save(payload: TaskPayload): void {
    if (this.saving()) return;

    const editing = this.editingTask();
    const request$ = editing ? this.tasksService.update(editing.id, payload) : this.tasksService.create(payload);

    this.saving.set(true);
    this.formErrors.set([]);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.closeForm();
        this.refresh();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.formErrors.set(toFriendlyErrors(error));
      }
    });
  }

  // ---- row actions ----

  toggleStatus(task: TaskItem): void {
    const next: TaskStatus = task.status === 'Done' ? 'Todo' : 'Done';
    this.runRowAction(task.id, this.tasksService.setStatus(task.id, next));
  }

  remove(task: TaskItem): void {
    if (!window.confirm(`Delete "${task.title}"? This cannot be undone.`)) return;
    this.runRowAction(task.id, this.tasksService.delete(task.id));
  }

  signOut(): void {
    this.auth.logout();
  }

  isBusy(id: number): boolean {
    return this.busyIds().has(id);
  }

  isOverdue(task: TaskItem): boolean {
    return task.status === 'Todo' && !!task.dueDate && task.dueDate < todayLocal();
  }

  /** Parse yyyy-MM-dd as a local date. `new Date('2026-10-10')` would be UTC and can show the day before. */
  toLocalDate(value: string): Date {
    const [y, m, d] = value.split('-').map(Number);
    return new Date(y, m - 1, d);
  }

  private runRowAction(id: number, request$: Observable<unknown>): void {
    if (this.isBusy(id)) return;

    this.setBusy(id, true);
    this.actionError.set(null);

    request$.subscribe({
      next: () => {
        this.setBusy(id, false);
        this.refresh();
      },
      error: (error: unknown) => {
        this.setBusy(id, false);
        this.actionError.set(toFriendlyError(error));
        // Item may have been deleted elsewhere (404) or changed: re-sync the list.
        this.refresh();
      }
    });
  }

  /** Re-fetch without the full-page spinner so the table does not flash after every action. */
  private refresh(): void {
    this.tasksService.list().subscribe({
      next: (tasks) => this.tasks.set(tasks),
      error: (error: unknown) => this.actionError.set(toFriendlyError(error))
    });
  }

  private setBusy(id: number, busy: boolean): void {
    this.busyIds.update((current) => {
      const next = new Set(current);
      if (busy) next.add(id);
      else next.delete(id);
      return next;
    });
  }
}

function todayLocal(): string {
  const now = new Date();
  const mm = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${mm}-${dd}`;
}
