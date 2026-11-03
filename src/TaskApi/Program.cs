using Microsoft.EntityFrameworkCore;
using TaskApi.Application.Interfaces;
using TaskApi.Application.Services;
using TaskApi.Infrastructure.Data;
using TaskApi.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is required.");

// AddDbContextの既定はScoped。1リクエストで同じDbContextを共有する。
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

var app = builder.Build();

// 未処理例外は500 ProblemDetailsに変換し、例外の詳細はログ側に残す。
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.Run();

// WebApplicationFactoryからエントリーポイントを参照するために公開する。
public partial class Program;
