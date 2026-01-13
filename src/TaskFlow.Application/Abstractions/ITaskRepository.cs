using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Abstractions;

public interface ITaskRepository
{
    IQueryable<TaskItem> QueryWithTags();
    Task<TaskItem?> GetByIdWithTagsAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(TaskItem task, CancellationToken ct = default);
    Task<TaskItem?> GetForUpdateAsync(Guid id, CancellationToken ct = default);
}
