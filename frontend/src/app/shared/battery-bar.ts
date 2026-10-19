import { Component, input } from '@angular/core';

@Component({
  selector: 'app-battery-bar',
  template: `
    @let value = level();
    @if (value === null) {
      –
    } @else {
      <span class="battery"><i [style.width.%]="value" [class.low]="value < 25"></i></span>{{ value }}%
    }
  `,
  styles: `
    .battery {
      display: inline-block;
      width: 60px;
      height: 8px;
      margin-right: 6px;
      border-radius: 4px;
      background: var(--grey-bg);
      vertical-align: middle;
    }
    .battery i {
      display: block;
      height: 100%;
      border-radius: 4px;
      background: var(--ok);
    }
    .battery i.low {
      background: var(--off);
    }
  `,
})
export class BatteryBar {
  readonly level = input.required<number | null>();
}
