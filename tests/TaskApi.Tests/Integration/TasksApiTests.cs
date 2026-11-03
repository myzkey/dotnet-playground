using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskApi.Contracts.Responses;
using TaskApi.Infrastructure.Data;
using TaskApi.Infrastructure.Repositories;

namespace TaskApi.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class TasksApiTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Crud_filter_completion_and_location_work_through_http_and_postgres()
    {
        await using var app = new TestApp();
        using var client = await app.CreateInitializedClientAsync(Token);
        using var created = await client.PostAsJsonAsync("/api/tasks", new
        {
            title = "  .NETを学ぶ  ",
            description = "HTTPからDBまで追う"
        }, Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = await created.Content.ReadFromJsonAsync<TaskResponse>(Token);
        Assert.NotNull(task);
        Assert.True(task.Id > 0);
        Assert.Equal(".NETを学ぶ", task.Title);
        Assert.False(task.IsCompleted);
        Assert.Equal(TimeSpan.Zero, task.CreatedAt.Offset);
        Assert.Equal(task.CreatedAt, task.UpdatedAt);
        Assert.NotNull(created.Headers.Location);
        Assert.EndsWith($"/api/tasks/{task.Id}", created.Headers.Location.ToString());

        var fetched = await client.GetFromJsonAsync<TaskResponse>(created.Headers.Location, Token);
        Assert.Equal(task, fetched); // 別リクエスト/DbContextで読み戻せる。
        Assert.Single((await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks?isCompleted=false", Token))!);
        Assert.Empty((await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks?isCompleted=true", Token))!);

        app.Clock.UtcNow = task.CreatedAt.AddMinutes(1);
        using var update = await client.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            title = " EF Coreを学ぶ ",
            isCompleted = false
        }, Token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<TaskResponse>(Token);
        Assert.NotNull(updated);
        Assert.Equal("EF Coreを学ぶ", updated.Title);
        Assert.Null(updated.Description);
        Assert.Equal(task.CreatedAt, updated.CreatedAt);
        Assert.Equal(app.Clock.UtcNow, updated.UpdatedAt);

        app.Clock.UtcNow = task.CreatedAt.AddMinutes(2);
        using var complete = await client.PatchAsync($"/api/tasks/{task.Id}/complete", null, Token);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = await complete.Content.ReadFromJsonAsync<TaskResponse>(Token);
        Assert.NotNull(completed);
        Assert.True(completed.IsCompleted);
        app.Clock.UtcNow = task.CreatedAt.AddHours(1);
        using var repeated = await client.PatchAsync($"/api/tasks/{task.Id}/complete", null, Token);
        Assert.Equal(completed, await repeated.Content.ReadFromJsonAsync<TaskResponse>(Token));
        Assert.Single((await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks?isCompleted=true", Token))!);
        Assert.Empty((await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks?isCompleted=false", Token))!);

        using var delete = await client.DeleteAsync($"/api/tasks/{task.Id}", Token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(await delete.Content.ReadAsByteArrayAsync(Token));
        using var afterDelete = await client.GetAsync($"/api/tasks/{task.Id}", Token);
        await AssertProblemAsync(afterDelete, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"title\":null}")]
    [InlineData("{\"title\":\"\"}")]
    [InlineData("{\"title\":\"   \"}")]
    [InlineData("{\"title\":\"\\t\\n\"}")]
    [InlineData("{\"title\":123}")]
    [InlineData("null")]
    [InlineData("{invalid-json")]
    public async Task Invalid_post_returns_validation_problem_without_writing(string json)
    {
        await using var app = new TestApp();
        using var client = await app.CreateInitializedClientAsync(Token);
        using var response = await client.PostAsync("/api/tasks", Json(json), Token);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, validation: true);
        Assert.Empty((await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks", Token))!);
    }

    [Theory]
    [InlineData(200, HttpStatusCode.Created)]
    [InlineData(201, HttpStatusCode.BadRequest)]
    public async Task Title_length_boundary_is_enforced(int length, HttpStatusCode expected)
    {
        await using var app = new TestApp();
        using var client = await app.CreateInitializedClientAsync(Token);
        using var response = await client.PostAsJsonAsync("/api/tasks", new { title = new string('a', length) }, Token);
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"title\":\"更新\"}")]
    [InlineData("{\"title\":\"更新\",\"isCompleted\":null}")]
    [InlineData("{\"title\":\" \",\"isCompleted\":false}")]
    public async Task Invalid_put_returns_validation_problem(string json)
    {
        await using var app = new TestApp();
        using var client = await app.CreateInitializedClientAsync(Token);
        using var response = await client.PutAsync("/api/tasks/1", Json(json), Token);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, validation: true);
    }

    [Theory]
    [InlineData("GET", "/api/tasks/999")]
    [InlineData("PUT", "/api/tasks/999")]
    [InlineData("DELETE", "/api/tasks/999")]
    [InlineData("PATCH", "/api/tasks/999/complete")]
    public async Task Missing_task_returns_404_problem(string method, string path)
    {
        await using var app = new TestApp();
        using var client = await app.CreateInitializedClientAsync(Token);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "PUT")
        {
            request.Content = Json("{\"title\":\"更新\",\"isCompleted\":false}");
        }
        using var response = await client.SendAsync(request, Token);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_filter_returns_400_and_unknown_route_returns_404_problem()
    {
        await using var app = new TestApp();
        using var client = await app.CreateInitializedClientAsync(Token);
        using var invalidFilter = await client.GetAsync("/api/tasks?isCompleted=maybe", Token);
        await AssertProblemAsync(invalidFilter, HttpStatusCode.BadRequest, validation: true);
        using var unknownRoute = await client.GetAsync("/unknown", Token);
        await AssertProblemAsync(unknownRoute, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Openapi_describes_all_six_operations_and_validation_constraints()
    {
        await using var app = new TestApp();
        using var client = await app.CreateInitializedClientAsync(Token);
        using var response = await client.GetAsync("/openapi/v1.json", Token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        var root = document.RootElement;
        Assert.StartsWith("3.1", root.GetProperty("openapi").GetString()!);
        var paths = root.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/tasks").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/tasks").TryGetProperty("post", out _));
        foreach (var method in new[] { "get", "put", "delete" })
        {
            Assert.True(paths.GetProperty("/api/tasks/{id}").TryGetProperty(method, out _));
        }
        Assert.True(paths.GetProperty("/api/tasks/{id}/complete").TryGetProperty("patch", out _));
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var create = schemas.GetProperty("CreateTaskRequest");
        Assert.Equal(200, create.GetProperty("properties").GetProperty("title").GetProperty("maxLength").GetInt32());
        Assert.Contains("title", create.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("isCompleted", schemas.GetProperty("UpdateTaskRequest").GetProperty("required")
            .EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task Migration_is_applied_and_repository_list_does_not_track_entities()
    {
        await using var app = new TestApp();
        using var client = await app.CreateInitializedClientAsync(Token);
        using var created = await client.PostAsJsonAsync("/api/tasks", new { title = "追跡を観察" }, Token);
        created.EnsureSuccessStatusCode();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.Database.GetAppliedMigrationsAsync(Token));
        var repository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var tasks = await repository.GetAllAsync(null, Token);
        Assert.Single(tasks);
        Assert.Empty(db.ChangeTracker.Entries());
        await repository.GetByIdAsync(tasks[0].Id, Token);
        Assert.Single(db.ChangeTracker.Entries());
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task AssertProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, bool validation = false)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal((int)status, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("title").GetString()));
        if (validation)
        {
            Assert.True(document.RootElement.GetProperty("errors").EnumerateObject().Any());
        }
    }
}
