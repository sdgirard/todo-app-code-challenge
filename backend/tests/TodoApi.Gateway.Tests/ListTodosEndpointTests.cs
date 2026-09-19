using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using TodoApi.Todos.Models;
using TodoApi.Todos.Persistence;

namespace TodoApi.Gateway.Tests;

public class ListTodosEndpointTests : IDisposable
{
    private readonly TodoApiWebApplicationFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Mock<ITodoRepository> _repository;

    public ListTodosEndpointTests()
    {
        _client = _factory.CreateClient();
        _repository = _factory.Repository;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // Stored todos should come back with their details intact. Looks each one up by
    // id rather than by position — the endpoint doesn't promise any ordering yet
    // (see the list-todos spec).
    [Fact]
    public async Task GetTodos_WithStoredTodos_ReturnsThemWithTheirDetails()
    {
        var first = CreateTodo("Buy milk", description: "2% or whole");
        var second = CreateTodo("Renew passport", description: null);
        StoredTodosAre(first, second);

        var response = await _client.GetAsync("/todos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(2, body.GetArrayLength());

        var firstJson = FindById(body, first.Id);
        Assert.Equal("Buy milk", firstJson.GetProperty("title").GetString());
        Assert.Equal("2% or whole", firstJson.GetProperty("description").GetString());
        Assert.False(firstJson.GetProperty("isCompleted").GetBoolean());

        var secondJson = FindById(body, second.Id);
        Assert.Equal("Renew passport", secondJson.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, secondJson.GetProperty("description").ValueKind);
    }

    // With nothing stored, listing todos should succeed and return an empty list —
    // an empty to-do list isn't an error.
    [Fact]
    public async Task GetTodos_WithNoTodos_ReturnsOkWithEmptyArray()
    {
        StoredTodosAre();

        var response = await _client.GetAsync("/todos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(0, body.GetArrayLength());
    }

    // A todo that's been edited should report when, and one that hasn't should
    // report nothing at all.
    [Fact]
    public async Task GetTodos_ReportsUpdatedAtOnlyForEditedTodos()
    {
        var edited = CreateTodo("Edited", description: null);
        edited.UpdatedAt = new DateTime(2026, 9, 19, 11, 5, 2, DateTimeKind.Utc);
        var neverEdited = CreateTodo("Never edited", description: null);
        StoredTodosAre(edited, neverEdited);

        var response = await _client.GetAsync("/todos");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            edited.UpdatedAt,
            FindById(body, edited.Id).GetProperty("updatedAt").GetDateTime());
        Assert.Equal(
            JsonValueKind.Null,
            FindById(body, neverEdited.Id).GetProperty("updatedAt").ValueKind);
    }

    private void StoredTodosAre(params TodoModel[] todos) =>
        _repository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(todos);

    private static TodoModel CreateTodo(string title, string? description) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Description = description,
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow,
    };

    private static JsonElement FindById(JsonElement body, Guid id) =>
        body.EnumerateArray().Single(todo => todo.GetProperty("id").GetGuid() == id);
}
