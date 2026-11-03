using TaskApi.Application.Services;
using TaskApi.Contracts.Requests;
using TaskApi.Domain.Entities;
using TaskApi.Tests.Doubles;

namespace TaskApi.Tests;

[Trait("Category", "Unit")]
public sealed class TaskServiceTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private readonly InMemoryTaskRepository _repository = new();
    private readonly StubTimeProvider _clock = new(InitialTime);
    private TaskService CreateService() => new(_repository, _clock);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_assigns_id_trims_title_and_sets_utc_timestamps()
    {
        var result = await CreateService().CreateAsync(new CreateTaskRequest
        {
            Title = "  C#を学ぶ  ",
            Description = "DIから試す"
        }, Token);

        Assert.True(result.Id > 0);
        Assert.Equal("C#を学ぶ", result.Title);
        Assert.Equal("DIから試す", result.Description);
        Assert.False(result.IsCompleted);
        Assert.Equal(InitialTime, result.CreatedAt);
        Assert.Equal(InitialTime, result.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, result.CreatedAt.Offset);
        Assert.Single(_repository.Items);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Get_returns_task_without_saving()
    {
        Seed();
        var result = await CreateService().GetByIdAsync(1, Token);
        Assert.NotNull(result);
        Assert.Equal("初期タイトル", result.Title);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    public async Task List_supports_optional_completion_filter(bool? completed, int count)
    {
        Seed();
        _repository.Items.Add(new TaskItem { Id = 2, Title = "完了済み", IsCompleted = true });
        var result = await CreateService().GetAllAsync(completed, Token);
        Assert.Equal(count, result.Count);
        if (completed is { } expected)
        {
            Assert.All(result, task => Assert.Equal(expected, task.IsCompleted));
        }
    }

    [Fact]
    public async Task Update_replaces_fields_preserves_created_at_and_advances_updated_at()
    {
        Seed();
        _clock.UtcNow = InitialTime.AddMinutes(10);
        var result = await CreateService().UpdateAsync(1, new UpdateTaskRequest
        {
            Title = " 新タイトル ",
            Description = null,
            IsCompleted = true
        }, Token);

        Assert.NotNull(result);
        Assert.Equal("新タイトル", result.Title);
        Assert.Null(result.Description);
        Assert.True(result.IsCompleted);
        Assert.Equal(InitialTime, result.CreatedAt);
        Assert.Equal(_clock.UtcNow, result.UpdatedAt);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Missing_task_returns_null_or_false_and_never_saves()
    {
        var service = CreateService();
        Assert.Null(await service.GetByIdAsync(999, Token));
        Assert.Null(await service.UpdateAsync(999, new UpdateTaskRequest { Title = "更新" }, Token));
        Assert.Null(await service.CompleteAsync(999, Token));
        Assert.False(await service.DeleteAsync(999, Token));
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Complete_changes_only_completion_and_updated_at()
    {
        Seed();
        _clock.UtcNow = InitialTime.AddMinutes(5);
        var result = await CreateService().CompleteAsync(1, Token);
        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
        Assert.Equal("初期タイトル", result.Title);
        Assert.Equal("説明", result.Description);
        Assert.Equal(InitialTime, result.CreatedAt);
        Assert.Equal(_clock.UtcNow, result.UpdatedAt);
    }

    [Fact]
    public async Task Complete_is_idempotent_including_updated_at()
    {
        Seed();
        var service = CreateService();
        var first = await service.CompleteAsync(1, Token);
        _clock.UtcNow = InitialTime.AddHours(1);
        var second = await service.CompleteAsync(1, Token);
        Assert.Equal(first, second); // recordの値による等価比較。
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Delete_removes_existing_task()
    {
        Seed();
        Assert.True(await CreateService().DeleteAsync(1, Token));
        Assert.Empty(_repository.Items);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("get")]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("complete")]
    [InlineData("delete")]
    public async Task Every_operation_forwards_cancellation_token(string operation)
    {
        Seed();
        using var source = new CancellationTokenSource();
        var token = source.Token;
        var service = CreateService();
        switch (operation)
        {
            case "list": await service.GetAllAsync(null, token); break;
            case "get": await service.GetByIdAsync(1, token); break;
            case "create": await service.CreateAsync(new CreateTaskRequest { Title = "追加" }, token); break;
            case "update": await service.UpdateAsync(1, new UpdateTaskRequest { Title = "更新" }, token); break;
            case "complete": await service.CompleteAsync(1, token); break;
            case "delete": await service.DeleteAsync(1, token); break;
        }
        Assert.NotEmpty(_repository.ReceivedTokens);
        Assert.All(_repository.ReceivedTokens, actual => Assert.Equal(token, actual));
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateService().GetByIdAsync(1, source.Token));
    }

    private void Seed() => _repository.Items.Add(new TaskItem
    {
        Id = 1,
        Title = "初期タイトル",
        Description = "説明",
        CreatedAt = InitialTime,
        UpdatedAt = InitialTime
    });
}
