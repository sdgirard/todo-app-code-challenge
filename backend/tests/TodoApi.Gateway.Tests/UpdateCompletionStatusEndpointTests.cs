using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Gateway.Tests;

public class UpdateCompletionStatusEndpointTests : IDisposable
{
    private readonly TodoApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Mock<ITodoRepository> _repository;

    public UpdateCompletionStatusEndpointTests()
    {
        _client = _factory.CreateClient();
        _repository = _factory.Repository;
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<TodoModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TodoModel todo, CancellationToken _) => todo);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // Marking an incomplete todo as completed should come back reflecting that,
    // with a freshly stamped updatedAt — the "Complete" half of the requirement.
    [Fact]
    public async Task PatchTodos_MarkingIncompleteTodoAsCompleted_ReturnsOkWithIsCompletedTrue()
    {
        var todo = CreateTodo("Buy milk", isCompleted: false);
        StoredTodoIs(todo.Id, todo);

        var response = await _client.PatchAsJsonAsync($"/todos/{todo.Id}", new { isCompleted = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(todo.Id, body.GetProperty("id").GetGuid());
        Assert.True(body.GetProperty("isCompleted").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("updatedAt").ValueKind);
    }

    // Marking a completed todo as not completed should come back reflecting that —
    // the "Incomplete" half of the requirement, exercised through the same endpoint.
    [Fact]
    public async Task PatchTodos_MarkingCompletedTodoAsIncomplete_ReturnsOkWithIsCompletedFalse()
    {
        var todo = CreateTodo("Buy milk", isCompleted: true);
        StoredTodoIs(todo.Id, todo);

        var response = await _client.PatchAsJsonAsync($"/todos/{todo.Id}", new { isCompleted = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isCompleted").GetBoolean());
    }

    // This endpoint only ever touches isCompleted (and updatedAt) — the other
    // fields should come back exactly as they were, distinguishing it from PUT.
    [Fact]
    public async Task PatchTodos_WithMatchingTodo_LeavesOtherFieldsUnchanged()
    {
        var todo = CreateTodo("Buy milk", isCompleted: false);
        todo.Description = "2% or whole";
        todo.DueDate = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);
        StoredTodoIs(todo.Id, todo);

        var response = await _client.PatchAsJsonAsync($"/todos/{todo.Id}", new { isCompleted = true });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Buy milk", body.GetProperty("title").GetString());
        Assert.Equal("2% or whole", body.GetProperty("description").GetString());
        Assert.Equal(todo.DueDate, body.GetProperty("dueDate").GetDateTime());
    }

    // Patching a todo that doesn't exist should come back as a clean 404, and
    // nothing downstream should be touched.
    [Fact]
    public async Task PatchTodos_WithNoMatchingTodo_ReturnsNotFoundAndDoesNotTouchRepositoryFurther()
    {
        var id = Guid.NewGuid();
        StoredTodoIs(id, todo: null);

        var response = await _client.PatchAsJsonAsync($"/todos/{id}", new { isCompleted = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repository.Verify(
            r => r.GetTrackedByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<TodoModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // A malformed id shouldn't reach the handler at all — routing itself should
    // reject it before any lookup happens.
    [Fact]
    public async Task PatchTodos_WithMalformedId_ReturnsNotFound()
    {
        var response = await _client.PatchAsJsonAsync("/todos/not-a-guid", new { isCompleted = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _repository.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // A valid request against an existing id should actually reach the
    // persistence layer with the requested completion status applied.
    [Fact]
    public async Task PatchTodos_WithMatchingTodo_PersistsTheRequestedCompletionStatus()
    {
        var todo = CreateTodo("Buy milk", isCompleted: false);
        StoredTodoIs(todo.Id, todo);

        await _client.PatchAsJsonAsync($"/todos/{todo.Id}", new { isCompleted = true });

        _repository.Verify(
            r => r.UpdateAsync(
                It.Is<TodoModel>(t => t.Id == todo.Id && t.IsCompleted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void StoredTodoIs(Guid id, TodoModel? todo)
    {
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(todo);
        _repository
            .Setup(r => r.GetTrackedByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(todo);
    }

    private static TodoModel CreateTodo(string title, bool isCompleted) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        IsCompleted = isCompleted,
        CreatedAt = DateTime.UtcNow,
    };
}
