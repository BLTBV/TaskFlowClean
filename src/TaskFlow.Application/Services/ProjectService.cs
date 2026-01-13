using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Services;

public class ProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _uow;

    public ProjectService(IProjectRepository projects, IUnitOfWork uow)
    {
        _projects = projects;
        _uow = uow;
    }

    public async Task<List<ProjectResponse>> GetAllAsync(CancellationToken ct = default)
    {
        return await _projects.Query()
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new ProjectResponse(p.Id, p.Name, p.Description, p.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest req, CancellationToken ct = default)
    {
        var project = new Project
        {
            Name = req.Name.Trim(),
            Description = req.Description?.Trim()
        };

        await _projects.AddAsync(project, ct);
        await _uow.SaveChangesAsync(ct);

        return new ProjectResponse(project.Id, project.Name, project.Description, project.CreatedAtUtc);
    }

    public async Task<ProjectResponse> UpdateAsync(Guid id, UpdateProjectRequest req, CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(id, ct)
                      ?? throw new KeyNotFoundException("Project not found.");

        project.Name = req.Name.Trim();
        project.Description = req.Description?.Trim();

        await _uow.SaveChangesAsync(ct);
        return new ProjectResponse(project.Id, project.Name, project.Description, project.CreatedAtUtc);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(id, ct)
                      ?? throw new KeyNotFoundException("Project not found.");

        _projects.Remove(project);
        await _uow.SaveChangesAsync(ct);
    }
}
