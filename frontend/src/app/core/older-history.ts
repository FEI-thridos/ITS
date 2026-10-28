import { signal } from '@angular/core';
import { Observable, finalize, map } from 'rxjs';
import { HistoryInterval, Page } from './models';

/** Number of newest history rows embedded by `includeHistory=true`; also the page size used for the history endpoints. */
export const HISTORY_PAGE_SIZE = 100;

/**
 * History rows older than the newest 100 embedded in the list, loaded per item on demand ("Load older")
 * from a paginated history endpoint.
 */
export class OlderHistory<K, T extends HistoryInterval> {
  private readonly loaded = signal(new Map<K, { rows: T[]; totalCount: number }>());
  private readonly pending = signal(new Set<K>());

  constructor(private readonly fetchPage: (key: K, page: number) => Observable<Page<T>>) {}

  /** Newest embedded rows plus the older rows loaded so far, newest first. */
  rows(key: K, newest: T[]): T[] {
    const loaded = this.loaded().get(key);
    return loaded ? mergeRows(newest, loaded.rows) : newest;
  }

  hasMore(key: K, newest: T[]): boolean {
    const loaded = this.loaded().get(key);
    return loaded ? this.rows(key, newest).length < loaded.totalCount : newest.length >= HISTORY_PAGE_SIZE;
  }

  isLoading(key: K): boolean {
    return this.pending().has(key);
  }

  /** Loads the page following the rows shown now; overlaps caused by newly added history rows are de-duplicated. */
  loadMore(key: K, newest: T[]): Observable<void> {
    const rows = this.rows(key, newest);
    this.setPending(key, true);
    return this.fetchPage(key, Math.floor(rows.length / HISTORY_PAGE_SIZE) + 1).pipe(
      map((page) => {
        const entry = { rows: mergeRows(rows, page.items), totalCount: page.totalCount };
        this.loaded.update((all) => new Map(all).set(key, entry));
      }),
      finalize(() => this.setPending(key, false)),
    );
  }

  clear(): void {
    this.loaded.set(new Map());
  }

  private setPending(key: K, pending: boolean): void {
    this.pending.update((all) => {
      const next = new Set(all);
      if (pending) {
        next.add(key);
      } else {
        next.delete(key);
      }
      return next;
    });
  }
}

/** Appends the rows of `older` that are not in `newer` yet (both newest first; history intervals of one item never overlap). */
function mergeRows<T extends HistoryInterval>(newer: T[], older: T[]): T[] {
  const id = (row: T) => `${row.validFrom}|${row.validTo}`;
  const known = new Set(newer.map(id));
  return [...newer, ...older.filter((row) => !known.has(id(row)))];
}
