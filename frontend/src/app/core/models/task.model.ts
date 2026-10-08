export type TaskPriority = 'Low' | 'Medium' | 'High';
export type TaskStatus = 'Todo' | 'Done';

export interface TaskItem {
  id: number;
  title: string;
  description: string | null;
  priority: TaskPriority;
  /** yyyy-MM-dd (calendar date, no time zone) */
  dueDate: string | null;
  status: TaskStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
  // add new property for officer name
  officerName: string | null;
}

export interface TaskPayload {
  title: string;
  description: string | null;
  priority: TaskPriority;
  dueDate: string | null;
  // add new property for officer name
  officerName: string | null;
}
