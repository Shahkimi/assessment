using TaskManager.Api.Domain;

namespace TaskManager.Api.Contracts.Tasks;

public record UpdateTaskStatusRequest(TaskItemStatus? Status);
