using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Api.Common;
using TaskManager.Api.Contracts.Tasks;
using TaskManager.Api.Domain;
using TaskManager.Api.Services;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Tests;

public class TaskServiceTests
{
    private readonly FakeTaskRepository _repo = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero));
    private readonly TaskService _service;

    public TaskServiceTests()
    {
        _service = new TaskService(
            _repo,
            new CreateTaskRequestValidator(),
            new UpdateTaskRequestValidator(),
            new UpdateTaskStatusRequestValidator(),
            _clock);
    }

    private static CreateTaskRequest NewTask(string title = "t") => new(title, null, TaskPriority.Low, null);

    [Fact]
    public async Task Create_trims_title_and_starts_as_todo()
    {
        var created = await _service.CreateAsync(
            new CreateTaskRequest("  Write docs  ", "  details ", TaskPriority.High, new DateOnly(2026, 10, 10)));

        Assert.Equal("Write docs", created.Title);
        Assert.Equal("details", created.Description);
        Assert.Equal(TaskItemStatus.Todo, created.Status);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, created.CreatedAtUtc);
        Assert.Single(_repo.Items);
    }

    [Fact]
    public async Task Create_stores_blank_description_as_null()
    {
        var created = await _service.CreateAsync(new CreateTaskRequest("t", "   ", TaskPriority.Low, null));
        Assert.Null(created.Description);
    }

    [Fact]
    public async Task Create_with_invalid_request_throws_and_saves_nothing()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(NewTask("")));

        Assert.Contains(ex.Errors, e => e.PropertyName == "Title");
        Assert.Empty(_repo.Items);
    }

    [Fact]
    public async Task Update_changes_fields_and_bumps_updated_timestamp()
    {
        var created = await _service.CreateAsync(NewTask("old"));
        _clock.Advance(TimeSpan.FromMinutes(5));

        var updated = await _service.UpdateAsync(
            created.Id, new UpdateTaskRequest("new", "desc", TaskPriority.High, new DateOnly(2026, 12, 1)));

        Assert.Equal("new", updated.Title);
        Assert.Equal(TaskPriority.High, updated.Priority);
        Assert.Equal(new DateOnly(2026, 12, 1), updated.DueDate);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.UpdatedAtUtc > created.UpdatedAtUtc);
    }

    [Fact]
    public async Task Update_does_not_touch_status()
    {
        var created = await _service.CreateAsync(NewTask());
        await _service.UpdateStatusAsync(created.Id, new UpdateTaskStatusRequest(TaskItemStatus.Done));

        var updated = await _service.UpdateAsync(created.Id, new UpdateTaskRequest("t2", null, TaskPriority.Low, null));

        Assert.Equal(TaskItemStatus.Done, updated.Status);
    }

    [Fact]
    public async Task Update_unknown_id_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(999, new UpdateTaskRequest("t", null, TaskPriority.Low, null)));
    }

    [Fact]
    public async Task UpdateStatus_is_idempotent()
    {
        var created = await _service.CreateAsync(NewTask());

        await _service.UpdateStatusAsync(created.Id, new UpdateTaskStatusRequest(TaskItemStatus.Done));
        var second = await _service.UpdateStatusAsync(created.Id, new UpdateTaskStatusRequest(TaskItemStatus.Done));

        Assert.Equal(TaskItemStatus.Done, second.Status);
    }

    [Fact]
    public async Task UpdateStatus_can_undo_back_to_todo()
    {
        var created = await _service.CreateAsync(NewTask());
        await _service.UpdateStatusAsync(created.Id, new UpdateTaskStatusRequest(TaskItemStatus.Done));

        var undone = await _service.UpdateStatusAsync(created.Id, new UpdateTaskStatusRequest(TaskItemStatus.Todo));

        Assert.Equal(TaskItemStatus.Todo, undone.Status);
    }

    [Fact]
    public async Task UpdateStatus_unknown_id_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateStatusAsync(5, new UpdateTaskStatusRequest(TaskItemStatus.Done)));
    }

    [Fact]
    public async Task Delete_removes_task()
    {
        var created = await _service.CreateAsync(NewTask());

        await _service.DeleteAsync(created.Id);

        Assert.Empty(_repo.Items);
    }

    [Fact]
    public async Task Delete_unknown_id_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(123));
    }

    [Fact]
    public async Task GetAll_maps_every_task()
    {
        await _service.CreateAsync(NewTask("a"));
        await _service.CreateAsync(NewTask("b"));

        var all = await _service.GetAllAsync();

        Assert.Equal(["a", "b"], all.Select(t => t.Title));
    }
}
