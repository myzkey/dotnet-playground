namespace TaskApi.Domain.Entities;

// System.Threading.Tasks.Taskとの名前の衝突を避けてTaskItemとする。
// EFが追跡するEntityは、値で比較するrecordではなくclassにする。
public sealed class TaskItem
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
