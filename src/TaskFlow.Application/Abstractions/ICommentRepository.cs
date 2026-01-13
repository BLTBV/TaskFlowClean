using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Abstractions;

public interface ICommentRepository
{
    Task AddAsync(Comment comment, CancellationToken ct = default);
}
