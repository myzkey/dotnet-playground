using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaskApi.Contracts.Requests;

public sealed record UpdateTaskRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    // boolの既定値falseとJSONプロパティの欠落を区別する。
    [JsonRequired]
    public bool IsCompleted { get; init; }
}
