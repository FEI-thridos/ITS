import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './models';

/** Turns an HTTP error (ProblemDetails or network failure) into a message for the user. */
export function problemMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Unexpected error.';
  }
  if (error.status === 0) {
    return 'The server is not reachable.';
  }
  const problem = error.error as ProblemDetails | null;
  if (problem && typeof problem === 'object') {
    const text = problem.detail || problem.title;
    if (text) {
      return text;
    }
  }
  return `Request failed (HTTP ${error.status}).`;
}
