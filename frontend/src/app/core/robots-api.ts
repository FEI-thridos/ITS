import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Robot, RobotHistory } from './models';

@Injectable({ providedIn: 'root' })
export class RobotsApi {
  private readonly http = inject(HttpClient);

  getRobots(): Observable<Robot[]> {
    return this.http.get<Robot[]>('/api/robots');
  }

  getHistory(robotId: number): Observable<RobotHistory[]> {
    return this.http.get<RobotHistory[]>(`/api/robots/${robotId}/history`);
  }
}
