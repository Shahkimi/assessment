import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { API_BASE_URL } from '../api.config';
import { AuthService } from '../auth/auth.service';

/**
 * A 401 from a protected call means the token is expired or invalid: clear it and send the user
 * to /login with a "session expired" hint. A 401 from the login call itself just means wrong credentials.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: unknown) => {
      const isLogin = req.url === `${API_BASE_URL}/auth/login`;
      if (error instanceof HttpErrorResponse && error.status === 401 && !isLogin) {
        auth.logout(false);
        void router.navigate(['/login'], {
          queryParams: { returnUrl: router.url, reason: 'expired' }
        });
      }
      return throwError(() => error);
    })
  );
};
