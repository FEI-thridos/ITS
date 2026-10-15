import { Routes } from '@angular/router';
import { Missions } from './features/missions/missions';
import { Robots } from './features/robots/robots';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'missions' },
  { path: 'missions', component: Missions, title: 'Missions' },
  { path: 'robots', component: Robots, title: 'Robots' },
];
