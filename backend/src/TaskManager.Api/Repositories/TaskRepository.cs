using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Repositories;

public class TaskRepository(AppDbContext db) : ITaskRepository
{
    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default)
    {
        // Open work first, then soonest due date (no due date last), then newest.
        // Status is stored as text, so order on the boolean rather than the raw string.
        return await db.Tasks
            .AsNoTracking()
            .OrderBy(t => t.Status == TaskItemStatus.Done)
            .ThenBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public Task<TaskItem?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(TaskItem task, CancellationToken ct = default)
    {
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken ct = default)
    {
        // Entity was loaded tracked via GetByIdAsync, so SaveChanges persists the changes.
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(TaskItem task, CancellationToken ct = default)
    {
        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
    }
}
