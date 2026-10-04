import { HttpErrorResponse } from '@angular/common/http';

/** Shape of the backend's RFC 7807 error body. */
interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Turns any failed HTTP call into short messages that are safe to show to a user. */
export function toFriendlyErrors(error: unknown): string[] {
  if (!(error instanceof HttpErrorResponse)) {
    return ['Something went wrong. Please try again.'];
  }

  if (error.status === 0) {
    return ['Cannot reach the server. Check your connection and try again.'];
  }

  const problem = (typeof error.error === 'object' ? error.error : null) as ProblemDetails | null;

  if (error.status === 400 && problem?.errors) {
    const messages = Object.values(problem.errors).flat();
    if (messages.length) return messages;
  }

  switch (error.status) {
    case 401:
      return [problem?.detail ?? 'Your session has expired. Please sign in again.'];
    case 403:
      return ['You do not have permission to do that.'];
    case 404:
      return [problem?.detail ?? 'That item no longer exists. Refresh the list.'];
    case 502:
    case 503:
    case 504:
      return ['The server is temporarily unavailable. Please try again in a moment.'];
    default:
      return error.status >= 500
        ? ['The server hit an error. Please try again later.']
        : [problem?.detail ?? 'The request could not be completed.'];
  }
}

export function toFriendlyError(error: unknown): string {
  return toFriendlyErrors(error).join(' ');
}
