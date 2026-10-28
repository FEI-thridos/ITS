import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, catchError, exhaustMap, switchMap, tap, timer } from 'rxjs';
import { Robot, RobotHistory } from '../../core/models';
import { OlderHistory } from '../../core/older-history';
import { problemMessage } from '../../core/problem-message';
import { RobotsApi } from '../../core/robots-api';
import { BatteryBar } from '../../shared/battery-bar';
import { StateBadge } from '../../shared/state-badge';

const REFRESH_INTERVAL_MS = 1000;

interface ViewSettings {
  poll: boolean;
  history: boolean;
}

@Component({
  selector: 'app-robots',
  imports: [DatePipe, StateBadge, BatteryBar],
  templateUrl: './robots.html',
})
export class Robots {
  private readonly api = inject(RobotsApi);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly autoRefresh = signal(true);
  protected readonly showHistory = signal(false);
  protected readonly robots = signal<Robot[]>([]);
  protected readonly lastRefresh = signal<Date | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly olderHistory = new OlderHistory<number, RobotHistory>((id, page) => this.api.getHistory(id, page));

  constructor() {
    const settings = computed<ViewSettings>(() => ({ poll: this.autoRefresh(), history: this.showHistory() }));
    let previous: ViewSettings | null = null;
    toObservable(settings)
      .pipe(
        switchMap((current) => {
          // While paused, reload once only when history was switched on (the robots loaded so far have none).
          const reload = !previous || (current.history && !previous.history);
          previous = current;
          const ticks = current.poll ? timer(0, REFRESH_INTERVAL_MS) : reload ? timer(0) : EMPTY;
          // exhaustMap skips ticks while a request is still running (no piled-up requests).
          return ticks.pipe(exhaustMap(() => this.load(current.history)));
        }),
        takeUntilDestroyed(),
      )
      .subscribe();
  }

  protected setShowHistory(checked: boolean): void {
    if (!checked) {
      this.olderHistory.clear();
    }
    this.showHistory.set(checked);
  }

  protected loadOlder(robot: Robot): void {
    this.olderHistory
      .loadMore(robot.id, robot.history ?? [])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ error: (error: unknown) => this.error.set(problemMessage(error)) });
  }

  private load(withHistory: boolean): Observable<unknown> {
    return this.api.getRobots(withHistory).pipe(
      tap((robots) => {
        this.robots.set(robots);
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
