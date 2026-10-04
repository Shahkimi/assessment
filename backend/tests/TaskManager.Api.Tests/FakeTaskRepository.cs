using TaskManager.Api.Domain;
using TaskManager.Api.Repositories;

namespace TaskManager.Api.Tests;

/// <summary>Hand-written in-memory repository: keeps the tests free of a mocking library.</summary>
public class FakeTaskRepository : ITaskRepository
{
    private readonly List<TaskItem> _items = [];
    private int _nextId = 1;

    public IReadOnlyList<TaskItem> Items => _items;

    public Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TaskItem>>(_items.ToList());

    public Task<TaskItem?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_items.FirstOrDefault(t => t.Id == id));

    public Task AddAsync(TaskItem task, CancellationToken ct = default)
    {
        task.Id = _nextId++;
        _items.Add(task);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TaskItem task, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteAsync(TaskItem task, CancellationToken ct = default)
    {
        _items.Remove(task);
        return Task.CompletedTask;
    }
}
