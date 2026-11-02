export type DraftShape = Record<string, string | null | undefined>;
export type MergeResult<T extends DraftShape> = {
  draft: T;
  kept: string[];
  collisions: string[];
  discarded: string[];
};
export const EDITABLE: string[];
export const TRANSITIONS: Record<string, string[]>;
export function scheduleEditable(status: string): boolean;
export function workflowOptions(status: string): string[];
export function editedFields(base: DraftShape, draft: DraftShape): string[];
export function isDirty(base: DraftShape, draft: DraftShape): boolean;
export function mergeLatest<T extends DraftShape>(base: DraftShape, draft: DraftShape, latest: T): MergeResult<T>;
export function serverChanged(base: { version: number; recordStatus: string },
  latest: { version: number; recordStatus: string }): boolean;
