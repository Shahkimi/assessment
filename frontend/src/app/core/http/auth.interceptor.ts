import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { API_BASE_URL } from '../api.config';
import { AuthService } from '../auth/auth.service';

/** Adds `Authorization: Bearer <jwt>` to API calls only, never to other origins, and never to login. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).token();
  const isApiCall = req.url.startsWith(`${API_BASE_URL}/`);
  const isLogin = req.url === `${API_BASE_URL}/auth/login`;

  if (!token || !isApiCall || isLogin) return next(req);

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
