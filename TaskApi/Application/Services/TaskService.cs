using TaskApi.Application.Interfaces;
using TaskApi.Contracts.Requests;
using TaskApi.Contracts.Responses;
using TaskApi.Domain.Entities;
using TaskApi.Infrastructure.Repositories;

namespace TaskApi.Application.Services;

// プライマリコンストラクタによるコンストラクタインジェクション。
public sealed class TaskService(ITaskRepository repository, TimeProvider timeProvider) : ITaskService
{
    public async Task<IReadOnlyList<TaskResponse>> GetAllAsync(
        bool? isCompleted, CancellationToken cancellationToken)
    {
        var tasks = await repository.GetAllAsync(isCompleted, cancellationToken);
        // ここではIEnumerable。DB取得後のオブジェクト→DTO変換でSQLではない。
        return tasks.Select(ToResponse).ToArray();
    }

    public async Task<TaskResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(id, cancellationToken);
        return task is null ? null : ToResponse(task);
    }

    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            IsCompleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        repository.Add(task);
        await repository.SaveChangesAsync(cancellationToken);
        return ToResponse(task);
    }

    public async Task<TaskResponse?> UpdateAsync(
        int id, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return null;
        }

        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.IsCompleted = request.IsCompleted;
        task.UpdatedAt = timeProvider.GetUtcNow();
        await repository.SaveChangesAsync(cancellationToken);
        return ToResponse(task);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return false;
        }

        repository.Remove(task);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TaskResponse?> CompleteAsync(int id, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return null;
        }

        // 再度完了しても日時を変えず、同じ状態を返す。
        if (!task.IsCompleted)
        {
            task.IsCompleted = true;
            task.UpdatedAt = timeProvider.GetUtcNow();
            await repository.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(task);
    }

    private static TaskResponse ToResponse(TaskItem task) => new(
        task.Id, task.Title, task.Description, task.IsCompleted, task.CreatedAt, task.UpdatedAt);
}
