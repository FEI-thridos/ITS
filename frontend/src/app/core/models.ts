// DTOs of the middleware REST API (system-definition §7).

export type RobotState = 'Available' | 'Busy' | 'Offline';

/** Validity interval of a history row (previous state). */
export interface HistoryInterval {
  validFrom: string;
  validTo: string;
}

export interface Robot {
  id: number;
  robotCode: string;
  robotType: string | null;
  status: number;
  statusName: string | null;
  state: RobotState;
  batteryLevel: number | null;
  mapCode: string | null;
  nodeCode: string | null;
  missionCode: string | null;
  updatedAt: string;
  /** Newest 100 previous states; only with `includeHistory=true`. */
  history?: RobotHistory[];
}

export interface RobotHistory extends HistoryInterval {
  robotCode: string;
  robotType: string | null;
  status: number;
  statusName: string | null;
  state: RobotState;
  batteryLevel: number | null;
  mapCode: string | null;
  nodeCode: string | null;
  missionCode: string | null;
}

export type MissionFilter = 'all' | 'active' | 'finished';

export interface Mission {
  id: number;
  missionCode: string;
  missionType: string;
  startNode: string;
  targetNode: string;
  robotId: number;
  robotCode: string;
  status: number;
  statusName: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  /** Newest 100 previous states; only with `includeHistory=true`. */
  history?: MissionHistory[];
}

export interface MissionHistory extends HistoryInterval {
  missionCode: string;
  missionType: string;
  startNode: string;
  targetNode: string;
  robotId: number;
  robotCode: string;
  status: number;
  statusName: string | null;
  createdAt: string;
  completedAt: string | null;
}

export interface DispatchRequest {
  robotId: number;
  startNode: string;
  targetNode: string;
}

/** Page of a paginated history endpoint. */
export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** RFC 7807 ProblemDetails; `errors` (camelCase field -> messages) only on 400 validation problems. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}
