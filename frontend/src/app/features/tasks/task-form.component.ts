import { Component, OnInit, inject, input, output } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';

import { TaskItem, TaskPayload, TaskPriority } from '../../core/models/task.model';

/** "   " passes Validators.required, so reject whitespace-only titles explicitly. */
function notBlank(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  return typeof value === 'string' && value.length > 0 && value.trim().length === 0 ? { blank: true } : null;
}

@Component({
  selector: 'app-task-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './task-form.component.html',
  styleUrl: './task-form.component.scss'
})
export class TaskFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);

  /** Task being edited; null/undefined means "create". */
  readonly task = input<TaskItem | null>(null);
  readonly saving = input(false);
  /** Messages returned by the server (e.g. validation) for the last submit. */
  readonly serverErrors = input<string[]>([]);

  readonly submitted = output<TaskPayload>();
  readonly cancelled = output<void>();

  readonly priorities: TaskPriority[] = ['Low', 'Medium', 'High'];

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200), notBlank]],
    description: ['', [Validators.maxLength(2000)]],
    priority: ['Medium' as TaskPriority, [Validators.required]],
    dueDate: [''],
    // add new property for officer name
    officerName: ['', [Validators.maxLength(100)]]
  });

  ngOnInit(): void {
    const task = this.task();
    if (task) {
      this.form.setValue({
        title: task.title,
        description: task.description ?? '',
        priority: task.priority,
        dueDate: task.dueDate ?? '',
        // add new property for officer name
        officerName: task.officerName ?? ''
      });
    }
  }

  get title() {
    return this.form.controls.title;
  }
  
  /** Get the officer name control. */
  get officerName() {
    return this.form.controls.officerName;
  }

  get description() {
    return this.form.controls.description;
  }

  submit(): void {
    if (this.saving()) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.submitted.emit({
      title: value.title.trim(),
      description: value.description.trim() || null,
      priority: value.priority,
      dueDate: value.dueDate || null,
      // add new property for officer name
      officerName: value.officerName.trim() || null
    });
  }
}
