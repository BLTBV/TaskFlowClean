using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class TagRepository(AppDbContext db) : ITagRepository
{
    public Task<List<Tag>> GetByNamesAsync(IEnumerable<string> names, CancellationToken ct = default)
    {
        var set = names.ToHashSet();
        return db.Tags.Where(t => set.Contains(t.Name)).ToListAsync(ct);
    }

    public Task AddRangeAsync(IEnumerable<Tag> tags, CancellationToken ct = default) =>
        db.Tags.AddRangeAsync(tags, ct);
}
