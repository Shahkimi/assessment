import { Routes } from '@angular/router';

import { authGuard, guestGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'tasks' },
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in · TaskFlow',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'tasks',
    canActivate: [authGuard],
    title: 'Tasks · TaskFlow',
    loadComponent: () => import('./features/tasks/task-list.component').then((m) => m.TaskListComponent)
  },
  { path: '**', redirectTo: 'tasks' }
];
