import { Injectable } from '@angular/core';

const TOKEN_KEY = 'taskflow.token';

/** Only place that touches localStorage, so swapping storage later is a one-file change. */
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  get(): string | null {
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch {
      return null; // storage blocked (private mode / policy): behave as logged out
    }
  }

  set(token: string): void {
    try {
      localStorage.setItem(TOKEN_KEY, token);
    } catch {
      /* ignore: user will simply have to log in again after refresh */
    }
  }

  clear(): void {
    try {
      localStorage.removeItem(TOKEN_KEY);
    } catch {
      /* ignore */
    }
  }
}
