using TaskApi.Domain.Entities;
using TaskApi.Infrastructure.Repositories;

namespace TaskApi.Tests.Doubles;

// EFの代用品ではなくServiceの振る舞いを検証するための小さなFake。
// SQL・制約・追跡の正しさは別の実DBテストで確認する。
internal sealed class InMemoryTaskRepository : ITaskRepository
{
    public List<TaskItem> Items { get; } = [];
    public List<CancellationToken> ReceivedTokens { get; } = [];
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<TaskItem>> GetAllAsync(bool? isCompleted, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        IReadOnlyList<TaskItem> items = Items
            .Where(item => isCompleted is null || item.IsCompleted == isCompleted)
            .OrderBy(item => item.Id).ToArray();
        return Task.FromResult(items);
    }

    public Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
    }

    public void Add(TaskItem task)
    {
        task.Id = Items.Count == 0 ? 1 : Items.Max(item => item.Id) + 1;
        Items.Add(task);
    }

    public void Remove(TaskItem task) => Items.Remove(task);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        SaveCount++;
        return Task.CompletedTask;
    }

    private void Observe(CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
