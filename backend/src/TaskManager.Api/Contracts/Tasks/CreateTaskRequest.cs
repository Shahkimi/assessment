using TaskManager.Api.Domain;

namespace TaskManager.Api.Contracts.Tasks;

public record CreateTaskRequest(
    string? Title,
    string? Description,
    TaskPriority? Priority,
    DateOnly? DueDate);
