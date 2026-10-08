namespace TaskManager.Api.Domain;

/// <summary>
/// A single to-do entry. Named TaskItem to avoid clashing with System.Threading.Tasks.Task.
/// </summary>
public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>Calendar date only (no time/timezone) so it never shifts between client and server.</summary>
    public DateOnly? DueDate { get; set; }

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? OfficerName { get; set; } // add new field for officer name
}
