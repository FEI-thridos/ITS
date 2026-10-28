import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MissionsApi } from '../../core/missions-api';
import { problemMessage } from '../../core/problem-message';

@Component({
  selector: 'app-cancel-dialog',
  template: `
    <div class="modal-bg">
      <div class="modal">
        <h2>Cancel mission</h2>
        @if (error(); as message) {
          <div class="error" role="alert">{{ message }}</div>
        }
        <p>
          Cancel mission <b>{{ missionCode() }}</b>?<br />
          <span class="muted">The mission will be force-cancelled (cancelMode = FORCE).</span>
        </p>
        <div class="actions">
          <button type="button" [disabled]="submitting()" (click)="closed.emit()">Back</button>
          <button type="button" class="primary danger" [disabled]="submitting()" (click)="confirm()">Cancel mission</button>
        </div>
      </div>
    </div>
  `,
})
export class CancelDialog {
  private readonly api = inject(MissionsApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly missionCode = input.required<string>();
  readonly cancelled = output<void>();
  readonly closed = output<void>();

  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);

  protected confirm(): void {
    this.submitting.set(true);
    this.error.set(null);
    this.api
      .cancel(this.missionCode())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.cancelled.emit(),
        error: (error: unknown) => {
          this.submitting.set(false);
          this.error.set(problemMessage(error));
        },
      });
  }
}
