import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DispatchRequest, Mission, MissionFilter, MissionHistory, Page } from './models';
import { HISTORY_PAGE_SIZE } from './older-history';

@Injectable({ providedIn: 'root' })
export class MissionsApi {
  private readonly http = inject(HttpClient);

  getMissions(filter: MissionFilter, includeHistory: boolean): Observable<Mission[]> {
    let params = new HttpParams();
    if (filter !== 'all') {
      params = params.set('state', filter);
    }
    if (includeHistory) {
      params = params.set('includeHistory', true);
    }
    return this.http.get<Mission[]>('/api/missions', { params });
  }

  getHistory(missionCode: string, page: number): Observable<Page<MissionHistory>> {
    return this.http.get<Page<MissionHistory>>(`/api/missions/${encodeURIComponent(missionCode)}/history`, {
      params: { page, pageSize: HISTORY_PAGE_SIZE },
    });
  }

  dispatch(request: DispatchRequest): Observable<Mission> {
    return this.http.post<Mission>('/api/missions', request);
  }

  cancel(missionCode: string): Observable<void> {
    return this.http.post<void>(`/api/missions/${encodeURIComponent(missionCode)}/cancel`, null);
  }
}
