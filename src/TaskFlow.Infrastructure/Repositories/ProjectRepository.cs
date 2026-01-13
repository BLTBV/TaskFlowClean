using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class ProjectRepository(AppDbContext db) : IProjectRepository
{
    public IQueryable<Project> Query() => db.Projects.AsQueryable();

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        db.Projects.AnyAsync(p => p.Id == id, ct);

    public Task AddAsync(Project project, CancellationToken ct = default) =>
        db.Projects.AddAsync(project, ct).AsTask();

    public void Remove(Project project) => db.Projects.Remove(project);
}
