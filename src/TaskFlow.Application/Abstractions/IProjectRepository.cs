using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Abstractions;

public interface IProjectRepository
{
    IQueryable<Project> Query();
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
    void Remove(Project project);
}
