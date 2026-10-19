// DTOs of the middleware REST API (system-definition §7).

export type RobotState = 'Available' | 'Busy' | 'Offline';

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
}

export interface RobotHistory {
  robotCode: string;
  robotType: string | null;
  status: number;
  statusName: string | null;
  state: RobotState;
  batteryLevel: number | null;
  mapCode: string | null;
  nodeCode: string | null;
  missionCode: string | null;
  validFrom: string;
  validTo: string;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
}
