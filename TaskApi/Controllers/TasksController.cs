using Microsoft.AspNetCore.Mvc;
using TaskApi.Application.Interfaces;
using TaskApi.Contracts.Requests;
using TaskApi.Contracts.Responses;

namespace TaskApi.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(ITaskService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TaskResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TaskResponse>>> GetAll(
        [FromQuery] bool? isCompleted, CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(isCompleted, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var task = await service.GetByIdAsync(id, cancellationToken);
        return task is null ? TaskNotFound(id) : Ok(task);
    }

    [HttpPost]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskResponse>> Create(
        [FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> Update(
        int id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await service.UpdateAsync(id, request, cancellationToken);
        return task is null ? TaskNotFound(id) : Ok(task);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? NoContent() : TaskNotFound(id);

    [HttpPatch("{id:int}/complete")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> Complete(int id, CancellationToken cancellationToken)
    {
        var task = await service.CompleteAsync(id, cancellationToken);
        return task is null ? TaskNotFound(id) : Ok(task);
    }

    private ObjectResult TaskNotFound(int id) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Task not found.",
        detail: $"Task with ID {id} does not exist.");
}
