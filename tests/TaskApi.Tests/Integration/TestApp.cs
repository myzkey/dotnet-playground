using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using TaskApi.Infrastructure.Data;
using TaskApi.Tests.Doubles;

namespace TaskApi.Tests.Integration;

// 1テストごとに一意なDBを作成する。本体のtaskapi DBは変更しない。
internal sealed class TestApp : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"taskapi_tests_{Guid.NewGuid():N}";
    private readonly string _adminConnection;
    private bool _databaseCreated;
    public string ConnectionString { get; }
    public StubTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));

    public TestApp()
    {
        var settings = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("TASKAPI_TEST_CONNECTION")
            ?? "Host=127.0.0.1;Port=55432;Database=postgres;Username=taskapi;Password=taskapi_local");
        settings.Database = "postgres";
        settings.Pooling = false;
        _adminConnection = settings.ConnectionString;
        settings.Database = _databaseName;
        ConnectionString = settings.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            // ProgramのDB設定だけを実PostgreSQLのテスト用DBへ置き換える。
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(ConnectionString));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public async Task<HttpClient> CreateInitializedClientAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_adminConnection);
            await connection.OpenAsync(cancellationToken);
            // 識別子はコードで生成した固定プレフィックス+GUIDだけ。
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _databaseCreated = true;
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                "結合テストにはPostgreSQLが必要です。docker compose up -d --wait postgres を実行してください。" +
                "DBなしの単体テスト: docker compose run --rm --no-deps test --filter-trait Category=Unit", exception);
        }

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        return CreateClient();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_databaseCreated)
        {
            // テストのCancellationTokenが取り消されても後片付けは試す。
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using var connection = new NpgsqlConnection(_adminConnection);
            await connection.OpenAsync(cleanup.Token);
            await using var command = new NpgsqlCommand(
                $"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync(cleanup.Token);
        }
    }
}
