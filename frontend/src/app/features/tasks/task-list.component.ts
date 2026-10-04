import { Component, OnInit, inject, signal } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';
import { toFriendlyError } from '../../core/http/api-error';
import { TaskItem } from '../../core/models/task.model';
import { TasksService } from './tasks.service';

@Component({
  selector: 'app-task-list',
  standalone: true,
  templateUrl: './task-list.component.html',
  styleUrl: './task-list.component.scss'
})
export class TaskListComponent implements OnInit {
  private readonly tasksService = inject(TasksService);
  private readonly auth = inject(AuthService);

  readonly tasks = signal<TaskItem[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

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

  signOut(): void {
    this.auth.logout();
  }
}
