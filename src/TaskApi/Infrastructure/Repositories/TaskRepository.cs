using Microsoft.EntityFrameworkCore;
using TaskApi.Domain.Entities;
using TaskApi.Infrastructure.Data;

namespace TaskApi.Infrastructure.Repositories;

public sealed class TaskRepository(AppDbContext dbContext) : ITaskRepository
{
    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(
        bool? isCompleted, CancellationToken cancellationToken)
    {
        // IQueryableのLINQはこの時点では未実行。ToListAsyncでSQLになる。
        var query = dbContext.Tasks.AsNoTracking();
        if (isCompleted is { } completed)
        {
            query = query.Where(item => item.IsCompleted == completed);
        }

        return await query.OrderBy(item => item.Id).ToListAsync(cancellationToken);
    }

    public Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Tasks.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    // 通常のIdentity採番ではAddにI/Oがないため、無理にasyncにしない。
    public void Add(TaskItem task) => dbContext.Tasks.Add(task);

    public void Remove(TaskItem task) => dbContext.Tasks.Remove(task);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
