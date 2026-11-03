using System.ComponentModel.DataAnnotations;

namespace TaskApi.Contracts.Requests;

public sealed record CreateTaskRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }
}
