import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    title: 'Sign in · TaskFlow',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent)
  },
  { path: '**', redirectTo: 'login' }
];
