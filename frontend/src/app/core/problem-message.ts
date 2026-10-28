import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './models';

/** Turns an HTTP error (ProblemDetails incl. validation errors, or network failure) into a message for the user. */
export function problemMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Unexpected error.';
  }
  if (error.status === 0) {
    return 'The server is not reachable.';
  }
  const problem = asProblem(error);
  const text = [problem?.detail || problem?.title, ...Object.values(problem?.errors ?? {}).flat()]
    .filter(Boolean)
    .join(' ');
  return text || `Request failed (HTTP ${error.status}).`;
}

/** Field messages of a 400 ValidationProblemDetails (keyed by camelCase field), otherwise null. */
export function validationErrors(error: unknown): Record<string, string[]> | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return null;
  }
  return asProblem(error)?.errors ?? null;
}

function asProblem(error: HttpErrorResponse): ProblemDetails | null {
  return error.error && typeof error.error === 'object' ? (error.error as ProblemDetails) : null;
}
