using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Abstractions;

public interface ITagRepository
{
    Task<List<Tag>> GetByNamesAsync(IEnumerable<string> names, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Tag> tags, CancellationToken ct = default);
}
