import { Component, computed, input } from '@angular/core';

/** Active mission statuses (Created, Executing, Waiting, Cancelling, Warning). */
const ACTIVE_STATUSES = [10, 20, 25, 28, 50];

function badgeClass(status: number): string {
  if (status === 30 || status === 35) {
    return 'b-ok';
  }
  if (status === 31 || status === 60) {
    return 'b-off';
  }
  if (status === 50) {
    return 'b-busy';
  }
  return ACTIVE_STATUSES.includes(status) ? 'b-info' : 'b-grey';
}

@Component({
  selector: 'app-mission-status-badge',
  template: `<span class="badge" [class]="badgeClass()">{{ statusName() ?? '–' }} ({{ status() }})</span>`,
})
export class MissionStatusBadge {
  readonly status = input.required<number>();
  readonly statusName = input.required<string | null>();
  protected readonly badgeClass = computed(() => badgeClass(this.status()));
}
