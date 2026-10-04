import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { API_BASE_URL } from '../api.config';
import { LoginRequest, LoginResponse } from '../models/auth.model';
import { TokenStorageService } from './token-storage.service';

/** Reads the `exp` claim (seconds since epoch) without verifying the signature; the server does that. */
function tokenExpiryMs(token: string): number | null {
  try {
    const payload = token.split('.')[1];
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const exp = JSON.parse(json).exp;
    return typeof exp === 'number' ? exp * 1000 : null;
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly storage = inject(TokenStorageService);

  private readonly tokenSignal = signal<string | null>(this.storage.get());

  readonly token = this.tokenSignal.asReadonly();

  /** False once the stored token is past its expiry, so a stale token never counts as "logged in". */
  readonly isAuthenticated = computed(() => {
    const token = this.tokenSignal();
    if (!token) return false;
    const expiry = tokenExpiryMs(token);
    return expiry === null || expiry > Date.now();
  });

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${API_BASE_URL}/auth/login`, request).pipe(
      tap((response) => {
        this.storage.set(response.token);
        this.tokenSignal.set(response.token);
      })
    );
  }

  logout(redirectToLogin = true, returnUrl?: string): void {
    this.storage.clear();
    this.tokenSignal.set(null);
    if (redirectToLogin) {
      void this.router.navigate(['/login'], returnUrl ? { queryParams: { returnUrl } } : undefined);
    }
  }
}
