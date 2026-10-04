using TaskManager.Api.Domain;

namespace TaskManager.Api.Contracts.Tasks;

public record UpdateTaskRequest(
    string? Title,
    string? Description,
    TaskPriority? Priority,
    DateOnly? DueDate);
