using TaskApi.Domain.Entities;

namespace TaskApi.Infrastructure.Repositories;

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> GetAllAsync(bool? isCompleted, CancellationToken cancellationToken);
    // 単体取得は追跡あり。Serviceの変更をSaveChangesAsyncで保存できる。
    Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken);
    void Add(TaskItem task);
    void Remove(TaskItem task);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
