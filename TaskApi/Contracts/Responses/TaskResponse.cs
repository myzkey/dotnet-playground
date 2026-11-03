namespace TaskApi.Contracts.Responses;

public sealed record TaskResponse(
    int Id,
    string Title,
    string? Description,
    bool IsCompleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
