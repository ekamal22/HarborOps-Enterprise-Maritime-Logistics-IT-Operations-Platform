export interface BaselineStatus {
  service: string;
  datasetVersion: string;
  seededAtUtc: string;
  source: string;
}

export function parseBaselineStatus(payload: unknown): BaselineStatus;
