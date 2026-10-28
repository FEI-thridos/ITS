import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Page, Robot, RobotHistory } from './models';
import { HISTORY_PAGE_SIZE } from './older-history';

@Injectable({ providedIn: 'root' })
export class RobotsApi {
  private readonly http = inject(HttpClient);

  getRobots(includeHistory = false): Observable<Robot[]> {
    return this.http.get<Robot[]>('/api/robots', { params: includeHistory ? { includeHistory } : {} });
  }

  getHistory(robotId: number, page: number): Observable<Page<RobotHistory>> {
    return this.http.get<Page<RobotHistory>>(`/api/robots/${robotId}/history`, {
      params: { page, pageSize: HISTORY_PAGE_SIZE },
    });
  }
}
