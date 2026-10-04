using FluentValidation;
using TaskManager.Api.Common;
using TaskManager.Api.Contracts.Tasks;
using TaskManager.Api.Domain;
using TaskManager.Api.Repositories;

namespace TaskManager.Api.Services;

public class TaskService(
    ITaskRepository repository,
    IValidator<CreateTaskRequest> createValidator,
    IValidator<UpdateTaskRequest> updateValidator,
    IValidator<UpdateTaskStatusRequest> statusValidator,
    TimeProvider clock) : ITaskService
{
    public async Task<IReadOnlyList<TaskResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var tasks = await repository.GetAllAsync(ct);
        return tasks.Select(TaskResponse.From).ToList();
    }

    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var now = clock.GetUtcNow().UtcDateTime;
        var task = new TaskItem
        {
            Title = request.Title!.Trim(),
            Description = NormalizeDescription(request.Description),
            Priority = request.Priority!.Value,
            DueDate = request.DueDate,
            Status = TaskItemStatus.Todo,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(task, ct);
        return TaskResponse.From(task);
    }

    public async Task<TaskResponse> UpdateAsync(int id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var task = await GetOrThrowAsync(id, ct);
        task.Title = request.Title!.Trim();
        task.Description = NormalizeDescription(request.Description);
        task.Priority = request.Priority!.Value;
        task.DueDate = request.DueDate;
        task.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;

        await repository.UpdateAsync(task, ct);
        return TaskResponse.From(task);
    }

    public async Task<TaskResponse> UpdateStatusAsync(int id, UpdateTaskStatusRequest request, CancellationToken ct = default)
    {
        await statusValidator.ValidateAndThrowAsync(request, ct);

        var task = await GetOrThrowAsync(id, ct);
        task.Status = request.Status!.Value;
        task.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;

        await repository.UpdateAsync(task, ct);
        return TaskResponse.From(task);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var task = await GetOrThrowAsync(id, ct);
        await repository.DeleteAsync(task, ct);
    }

    private async Task<TaskItem> GetOrThrowAsync(int id, CancellationToken ct) =>
        await repository.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(TaskItem), id);

    private static string? NormalizeDescription(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
