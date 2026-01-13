using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class TaskRepository(AppDbContext db) : ITaskRepository
{
    public IQueryable<TaskItem> QueryWithTags() =>
        db.Tasks.Include(t => t.TaskTags).ThenInclude(tt => tt.Tag);

    public Task<TaskItem?> GetByIdWithTagsAsync(Guid id, CancellationToken ct = default) =>
        db.Tasks.AsNoTracking()
            .Include(t => t.TaskTags).ThenInclude(tt => tt.Tag)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        db.Tasks.AnyAsync(t => t.Id == id, ct);

    public Task AddAsync(TaskItem task, CancellationToken ct = default) =>
        db.Tasks.AddAsync(task, ct).AsTask();

    public Task<TaskItem?> GetForUpdateAsync(Guid id, CancellationToken ct = default) =>
        db.Tasks.Include(t => t.TaskTags).ThenInclude(tt => tt.Tag)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
}
