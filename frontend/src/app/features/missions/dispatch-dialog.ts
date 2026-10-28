import { Component, DestroyRef, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MissionsApi } from '../../core/missions-api';
import { Mission, Robot } from '../../core/models';
import { problemMessage, validationErrors } from '../../core/problem-message';
import { RobotsApi } from '../../core/robots-api';

type Field = 'robotId' | 'startNode' | 'targetNode';
const FIELDS: Field[] = ['robotId', 'startNode', 'targetNode'];

@Component({
  selector: 'app-dispatch-dialog',
  template: `
    <div class="modal-bg">
      <form class="modal" novalidate (submit)="$event.preventDefault(); submit()">
        <h2>Dispatch mission (RACK_MOVE)</h2>
        @if (error(); as message) {
          <div class="error" role="alert">{{ message }}</div>
        }
        <div class="field">
          <label for="f-robot">Robot *</label>
          <select #robot id="f-robot" [class.invalid]="fieldErrors().robotId" (change)="robotId.set(+robot.value || null)">
            <option value="">– select robot –</option>
            @for (r of robots(); track r.id) {
              <option [value]="r.id">{{ r.robotCode }} · {{ r.robotType ?? '–' }} · {{ r.state }}</option>
            }
          </select>
          @for (message of fieldErrors().robotId; track $index) {
            <small class="invalid-text">{{ message }}</small>
          } @empty {
            <small>Only robots stored in the database.</small>
          }
        </div>
        <div class="field">
          <label for="f-start">Start node *</label>
          <input #start id="f-start" maxlength="64" placeholder="M001-A001-45"
            [class.invalid]="fieldErrors().startNode" (input)="startNode.set(start.value)" />
          @for (message of fieldErrors().startNode; track $index) {
            <small class="invalid-text">{{ message }}</small>
          }
        </div>
        <div class="field">
          <label for="f-target">Target node *</label>
          <input #target id="f-target" maxlength="64" placeholder="M001-A001-40"
            [class.invalid]="fieldErrors().targetNode" (input)="targetNode.set(target.value)" />
          @for (message of fieldErrors().targetNode; track $index) {
            <small class="invalid-text">{{ message }}</small>
          }
        </div>
        <div class="actions">
          <button type="button" [disabled]="submitting()" (click)="closed.emit()">Cancel</button>
          <button class="primary" type="submit" [disabled]="submitting()">Dispatch</button>
        </div>
      </form>
    </div>
  `,
})
export class DispatchDialog {
  private readonly missionsApi = inject(MissionsApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly dispatched = output<Mission>();
  readonly closed = output<void>();

  protected readonly robots = signal<Robot[]>([]);
  protected readonly robotId = signal<number | null>(null);
  protected readonly startNode = signal('');
  protected readonly targetNode = signal('');
  protected readonly fieldErrors = signal<Partial<Record<Field, string[]>>>({});
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);

  constructor() {
    inject(RobotsApi)
      .getRobots()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (robots) => this.robots.set(robots),
        error: (error: unknown) => this.error.set(`Robots could not be loaded: ${problemMessage(error)}`),
      });
  }

  protected submit(): void {
    const robotId = this.robotId();
    const startNode = this.startNode().trim();
    const targetNode = this.targetNode().trim();
    const required: Partial<Record<Field, string[]>> = {};
    if (robotId === null) {
      required.robotId = ['Robot is required.'];
    }
    if (!startNode) {
      required.startNode = ['Start node is required.'];
    }
    if (!targetNode) {
      required.targetNode = ['Target node is required.'];
    }
    this.fieldErrors.set(required);
    this.error.set(null);
    if (robotId === null || !startNode || !targetNode) {
      return;
    }

    this.submitting.set(true);
    this.missionsApi
      .dispatch({ robotId, startNode, targetNode })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (mission) => this.dispatched.emit(mission),
        error: (error: unknown) => {
          this.submitting.set(false);
          this.showError(error);
        },
      });
  }

  /** 400 field errors go under their fields; everything else (incl. 502 / 503) is shown on top of the dialog. */
  private showError(error: unknown): void {
    const errors = validationErrors(error);
    if (errors && Object.keys(errors).every((key) => FIELDS.includes(key as Field))) {
      this.fieldErrors.set(errors);
    } else {
      this.error.set(problemMessage(error));
    }
  }
}
