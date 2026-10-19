import { Component, computed, input } from '@angular/core';
import { RobotState } from '../core/models';

const BADGE_CLASS: Record<RobotState, string> = {
  Available: 'b-ok',
  Busy: 'b-busy',
  Offline: 'b-off',
};

@Component({
  selector: 'app-state-badge',
  template: `<span class="badge" [class]="badgeClass()">{{ state() }}</span>`,
})
export class StateBadge {
  readonly state = input.required<RobotState>();
  protected readonly badgeClass = computed(() => BADGE_CLASS[this.state()]);
}
