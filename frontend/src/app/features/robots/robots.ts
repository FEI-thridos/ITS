import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, catchError, exhaustMap, forkJoin, map, of, switchMap, tap, timer } from 'rxjs';
import { Robot, RobotHistory } from '../../core/models';
import { problemMessage } from '../../core/problem-message';
import { RobotsApi } from '../../core/robots-api';
import { BatteryBar } from '../../shared/battery-bar';
import { StateBadge } from '../../shared/state-badge';

const REFRESH_INTERVAL_MS = 1000;

@Component({
  selector: 'app-robots',
  imports: [DatePipe, StateBadge, BatteryBar],
  templateUrl: './robots.html',
})
export class Robots {
  private readonly api = inject(RobotsApi);

  protected readonly autoRefresh = signal(true);
  protected readonly showHistory = signal(false);
  protected readonly robots = signal<Robot[]>([]);
  /** History per robot id; null while Show history is off or not loaded yet. */
  protected readonly histories = signal<Map<number, RobotHistory[]> | null>(null);
  protected readonly lastRefresh = signal<Date | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    const settings = computed(() => ({ poll: this.autoRefresh(), history: this.showHistory() }));
    toObservable(settings)
      .pipe(
        switchMap(({ poll, history }) => {
          // Polling on: tick every second. Polling off: load once only to show freshly requested history.
          const ticks = poll ? timer(0, REFRESH_INTERVAL_MS) : history && !this.histories() ? timer(0) : EMPTY;
          // exhaustMap skips ticks while a request is still running (no piled-up requests).
          return ticks.pipe(exhaustMap(() => this.load(history)));
        }),
        takeUntilDestroyed(),
      )
      .subscribe();
  }

  protected setShowHistory(checked: boolean): void {
    if (!checked) {
      this.histories.set(null);
    }
    this.showHistory.set(checked);
  }

  private load(withHistory: boolean): Observable<unknown> {
    return this.api.getRobots().pipe(
      switchMap((robots) => {
        const histories$: Observable<Map<number, RobotHistory[]> | null> = withHistory ? this.loadHistories(robots) : of(null);
        return histories$.pipe(map((histories) => ({ robots, histories })));
      }),
      tap(({ robots, histories }) => {
        this.robots.set(robots);
        this.histories.set(histories);
        this.lastRefresh.set(new Date());
        this.error.set(null);
      }),
      catchError((error: unknown) => {
        this.error.set(problemMessage(error));
        return EMPTY;
      }),
    );
  }

  private loadHistories(robots: Robot[]): Observable<Map<number, RobotHistory[]>> {
    if (robots.length === 0) {
      return of(new Map());
    }
    return forkJoin(robots.map((robot) => this.api.getHistory(robot.id))).pipe(
      map((histories) => new Map(robots.map((robot, i) => [robot.id, histories[i]]))),
    );
  }
}
