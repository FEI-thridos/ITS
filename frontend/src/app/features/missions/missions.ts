import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, Subject, catchError, exhaustMap, merge, switchMap, tap, timer } from 'rxjs';
import { MissionsApi } from '../../core/missions-api';
import { Mission, MissionFilter, MissionHistory } from '../../core/models';
import { OlderHistory } from '../../core/older-history';
import { problemMessage } from '../../core/problem-message';
import { MissionStatusBadge } from '../../shared/mission-status-badge';
import { CancelDialog } from './cancel-dialog';
import { DispatchDialog } from './dispatch-dialog';

const REFRESH_INTERVAL_MS = 1000;
const TOAST_DURATION_MS = 2500;
/** Statuses that may be force-cancelled (Created, Executing, Waiting, Warning); 28 Cancelling is already being cancelled. */
const CANCELABLE_STATUSES = [10, 20, 25, 50];

interface ViewSettings {
  poll: boolean;
  history: boolean;
  filter: MissionFilter;
}

@Component({
  selector: 'app-missions',
  imports: [DatePipe, MissionStatusBadge, DispatchDialog, CancelDialog],
  templateUrl: './missions.html',
})
export class Missions {
  private readonly api = inject(MissionsApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refresh$ = new Subject<void>();
  private toastTimer?: ReturnType<typeof setTimeout>;

  protected readonly filters: { value: MissionFilter; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'active', label: 'Active' },
    { value: 'finished', label: 'Finished' },
  ];
  protected readonly filter = signal<MissionFilter>('all');
  protected readonly autoRefresh = signal(true);
  protected readonly showHistory = signal(false);
  protected readonly missions = signal<Mission[]>([]);
  protected readonly lastRefresh = signal<Date | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly dispatchOpen = signal(false);
  /** Persistent until dismissed – not cleared by a successful refresh like `error`. */
  protected readonly dispatchError = signal<string | null>(null);
  protected readonly cancelCode = signal<string | null>(null);
  protected readonly toast = signal<string | null>(null);
  protected readonly olderHistory = new OlderHistory<string, MissionHistory>((code, page) => this.api.getHistory(code, page));

  constructor() {
    const settings = computed<ViewSettings>(() => ({
      poll: this.autoRefresh(),
      history: this.showHistory(),
      filter: this.filter(),
    }));
    let previous: ViewSettings | null = null;
    toObservable(settings)
      .pipe(
        switchMap((current) => {
          // While paused, reload once only when the change needs data the view does not have yet.
          const reload = !previous || current.filter !== previous.filter || (current.history && !previous.history);
          previous = current;
          const ticks = current.poll ? timer(0, REFRESH_INTERVAL_MS) : reload ? timer(0) : EMPTY;
          // exhaustMap skips ticks while a request is still running (no piled-up requests).
          return merge(ticks, this.refresh$).pipe(exhaustMap(() => this.load(current)));
        }),
        takeUntilDestroyed(),
      )
      .subscribe();
    this.destroyRef.onDestroy(() => clearTimeout(this.toastTimer));
  }

  protected isCancelable(mission: Mission): boolean {
    return CANCELABLE_STATUSES.includes(mission.status);
  }

  protected setShowHistory(checked: boolean): void {
    if (!checked) {
      this.olderHistory.clear();
    }
    this.showHistory.set(checked);
  }

  protected loadOlder(mission: Mission): void {
    this.olderHistory
      .loadMore(mission.missionCode, mission.history ?? [])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ error: (error: unknown) => this.error.set(problemMessage(error)) });
  }

  protected onDispatched(mission: Mission): void {
    this.dispatchOpen.set(false);
    this.showToast(`Mission ${mission.missionCode} dispatched`);
    this.refresh$.next();
  }

  protected onNotStored(message: string): void {
    this.dispatchOpen.set(false);
    this.dispatchError.set(message);
    this.refresh$.next();
  }

  protected onCancelled(missionCode: string): void {
    this.cancelCode.set(null);
    this.showToast(`Cancel request sent for ${missionCode}`);
    this.refresh$.next();
  }

  private showToast(message: string): void {
    clearTimeout(this.toastTimer);
    this.toast.set(message);
    this.toastTimer = setTimeout(() => this.toast.set(null), TOAST_DURATION_MS);
  }

  private load({ filter, history }: ViewSettings): Observable<unknown> {
    return this.api.getMissions(filter, history).pipe(
      tap((missions) => {
        this.missions.set(missions);
        this.lastRefresh.set(new Date());
        this.error.set(null);
      }),
      catchError((error: unknown) => {
        this.error.set(problemMessage(error));
        return EMPTY;
      }),
    );
  }
}
