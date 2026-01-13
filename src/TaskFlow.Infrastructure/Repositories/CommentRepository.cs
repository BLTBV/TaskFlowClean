using TaskFlow.Application.Abstractions;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class CommentRepository(AppDbContext db) : ICommentRepository
{
    public Task AddAsync(Comment comment, CancellationToken ct = default) =>
        db.Comments.AddAsync(comment, ct).AsTask();
}
