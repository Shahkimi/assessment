using TaskManager.Api.Domain;

namespace TaskManager.Api.Contracts.Tasks;

public record TaskResponse(
    int Id,
    string Title,
    string? Description,
    TaskPriority Priority,
    DateOnly? DueDate,
    TaskItemStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    // add new property for officer name
    string? OfficerName)
{
    public static TaskResponse From(TaskItem t) =>
        new(t.Id, t.Title, t.Description, t.Priority, t.DueDate, t.Status, t.CreatedAtUtc, t.UpdatedAtUtc, t.OfficerName);
}
