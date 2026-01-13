using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.Services;

public class TaskService
{
    private readonly ITaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly ITagRepository _tags;
    private readonly ICommentRepository _comments;
    private readonly IUnitOfWork _uow;

    public TaskService(
        ITaskRepository tasks,
        IProjectRepository projects,
        ITagRepository tags,
        ICommentRepository comments,
        IUnitOfWork uow)
    {
        _tasks = tasks;
        _projects = projects;
        _tags = tags;
        _comments = comments;
        _uow = uow;
    }

    private static readonly IReadOnlyDictionary<TaskStatus, TaskStatus[]> AllowedTransitions =
        new Dictionary<TaskStatus, TaskStatus[]>
        {
            [TaskStatus.Todo] = new[] { TaskStatus.InProgress, TaskStatus.Cancelled },
            [TaskStatus.InProgress] = new[] { TaskStatus.Done, TaskStatus.Cancelled },
            [TaskStatus.Done] = Array.Empty<TaskStatus>(),
            [TaskStatus.Cancelled] = Array.Empty<TaskStatus>()
        };

    public async Task<PagedResponse<TaskResponse>> SearchAsync(
        Guid? projectId,
        TaskStatusDto? status,
        TaskPriorityDto? priority,
        string? tag,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var q = _tasks.QueryWithTags().AsNoTracking();

        if (projectId.HasValue)
            q = q.Where(x => x.ProjectId == projectId.Value);

        if (status.HasValue)
        {
            var st = MapStatus(status.Value);
            q = q.Where(x => x.Status == st);
        }

        if (priority.HasValue)
        {
            var pr = MapPriority(priority.Value);
            q = q.Where(x => x.Priority == pr);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tg = tag.Trim().ToLowerInvariant();
            q = q.Where(x => x.TaskTags.Any(tt => tt.Tag.Name == tg));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x =>
                EF.Functions.ILike(x.Title, $"%{s}%") ||
                (x.Description != null && EF.Functions.ILike(x.Description, $"%{s}%")));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TaskResponse(
                x.Id,
                x.ProjectId,
                x.Title,
                x.Description,
                MapStatus(x.Status),
                MapPriority(x.Priority),
                x.DueDate,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.TaskTags.Select(tt => tt.Tag.Name).OrderBy(n => n).ToList()
            ))
            .ToListAsync(ct);

        return new PagedResponse<TaskResponse>(items, total, page, pageSize);
    }

    public async Task<TaskResponse> CreateAsync(CreateTaskRequest req, CancellationToken ct = default)
    {
        if (!await _projects.ExistsAsync(req.ProjectId, ct))
            throw new KeyNotFoundException("Project not found.");

        var task = new TaskItem
        {
            ProjectId = req.ProjectId,
            Title = req.Title.Trim(),
            Description = req.Description?.Trim(),
            Priority = MapPriority(req.Priority),
            DueDate = req.DueDate,
            Status = TaskStatus.Todo
        };

        if (req.Tags is { Count: > 0 })
        {
            var tags = await UpsertTagsAsync(req.Tags, ct);
            foreach (var t in tags)
                task.TaskTags.Add(new TaskTag { TagId = t.Id, TaskItemId = task.Id });
        }

        await _tasks.AddAsync(task, ct);
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(task.Id, ct);
    }

    public async Task<TaskResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _tasks.GetByIdWithTagsAsync(id, ct)
                   ?? throw new KeyNotFoundException("Task not found.");

        return new TaskResponse(
            task.Id,
            task.ProjectId,
            task.Title,
            task.Description,
            MapStatus(task.Status),
            MapPriority(task.Priority),
            task.DueDate,
            task.CreatedAtUtc,
            task.UpdatedAtUtc,
            task.TaskTags.Select(tt => tt.Tag.Name).OrderBy(n => n).ToList()
        );
    }

    public async Task<TaskResponse> UpdateAsync(Guid id, UpdateTaskRequest req, CancellationToken ct = default)
    {
        var task = await _tasks.GetForUpdateAsync(id, ct)
                   ?? throw new KeyNotFoundException("Task not found.");

        task.Title = req.Title.Trim();
        task.Description = req.Description?.Trim();
        task.Priority = MapPriority(req.Priority);
        task.DueDate = req.DueDate;
        task.UpdatedAtUtc = DateTime.UtcNow;

        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<TaskResponse> UpdateStatusAsync(Guid id, UpdateTaskStatusRequest req, CancellationToken ct = default)
    {
        var task = await _tasks.GetForUpdateAsync(id, ct)
                   ?? throw new KeyNotFoundException("Task not found.");

        var newStatus = MapStatus(req.Status);
        var currentStatus = task.Status;

        if (currentStatus == newStatus)
            return await GetByIdAsync(id, ct);

        if (!AllowedTransitions.TryGetValue(currentStatus, out var allowedNext) ||
            !allowedNext.Contains(newStatus))
        {
            throw new InvalidOperationException($"Status transition '{currentStatus} → {newStatus}' is not allowed.");
        }

        task.Status = newStatus;
        task.UpdatedAtUtc = DateTime.UtcNow;

        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<CommentResponse> AddCommentAsync(Guid taskId, AddCommentRequest req, CancellationToken ct = default)
    {
        if (!await _tasks.ExistsAsync(taskId, ct))
            throw new KeyNotFoundException("Task not found.");

        var c = new Comment { TaskItemId = taskId, Text = req.Text.Trim() };
        await _comments.AddAsync(c, ct);
        await _uow.SaveChangesAsync(ct);

        return new CommentResponse(c.Id, c.TaskItemId, c.Text, c.CreatedAtUtc);
    }

    public async Task<TaskResponse> SetTagsAsync(Guid taskId, List<string> tags, CancellationToken ct = default)
    {
        var task = await _tasks.GetForUpdateAsync(taskId, ct)
                   ?? throw new KeyNotFoundException("Task not found.");

        task.TaskTags.Clear();

        if (tags.Count > 0)
        {
            var upserted = await UpsertTagsAsync(tags, ct);
            foreach (var tg in upserted)
                task.TaskTags.Add(new TaskTag { TaskItemId = task.Id, TagId = tg.Id });
        }

        task.UpdatedAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(taskId, ct);
    }

    private async Task<List<Tag>> UpsertTagsAsync(List<string> rawTags, CancellationToken ct)
    {
        var normalized = rawTags
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .Take(20)
            .ToList();

        if (normalized.Count == 0) return new();

        var existing = await _tags.GetByNamesAsync(normalized, ct);
        var existingNames = existing.Select(e => e.Name).ToHashSet();

        var toAdd = normalized
            .Where(n => !existingNames.Contains(n))
            .Select(n => new Tag { Name = n })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _tags.AddRangeAsync(toAdd, ct);
            await _uow.SaveChangesAsync(ct);
            existing.AddRange(toAdd);
        }

        return existing;
    }

    private static TaskStatus MapStatus(TaskStatusDto dto) => dto switch
    {
        TaskStatusDto.Todo => TaskStatus.Todo,
        TaskStatusDto.InProgress => TaskStatus.InProgress,
        TaskStatusDto.Done => TaskStatus.Done,
        TaskStatusDto.Cancelled => TaskStatus.Cancelled,
        _ => throw new ArgumentException("Unknown status")
    };

    private static TaskStatusDto MapStatus(TaskStatus domain) => domain switch
    {
        TaskStatus.Todo => TaskStatusDto.Todo,
        TaskStatus.InProgress => TaskStatusDto.InProgress,
        TaskStatus.Done => TaskStatusDto.Done,
        TaskStatus.Cancelled => TaskStatusDto.Cancelled,
        _ => throw new ArgumentException("Unknown status")
    };

    private static TaskPriority MapPriority(TaskPriorityDto dto) => dto switch
    {
        TaskPriorityDto.Low => TaskPriority.Low,
        TaskPriorityDto.Medium => TaskPriority.Medium,
        TaskPriorityDto.High => TaskPriority.High,
        TaskPriorityDto.Critical => TaskPriority.Critical,
        _ => throw new ArgumentException("Unknown priority")
    };

    private static TaskPriorityDto MapPriority(TaskPriority domain) => domain switch
    {
        TaskPriority.Low => TaskPriorityDto.Low,
        TaskPriority.Medium => TaskPriorityDto.Medium,
        TaskPriority.High => TaskPriorityDto.High,
        TaskPriority.Critical => TaskPriorityDto.Critical,
        _ => throw new ArgumentException("Unknown priority")
    };
}
