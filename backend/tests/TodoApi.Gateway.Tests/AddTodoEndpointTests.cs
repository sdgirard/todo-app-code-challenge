using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TodoApi.Gateway.Tests;

public class AddTodoEndpointTests : IClassFixture<TodoApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AddTodoEndpointTests(TodoApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Sending a valid new todo through the real API should create it and hand back
    // a 201, a link to the new todo, and the todo's details in the response body.
    [Fact]
    public async Task PostTodos_WithValidBody_ReturnsCreatedWithLocationAndMatchingBody()
    {
        var request = new { title = "Buy milk", description = "2% or whole", dueDate = "2026-09-25T00:00:00Z" };

        var response = await _client.PostAsJsonAsync("/todos", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/todos/", response.Headers.Location!.ToString());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal("Buy milk", body.GetProperty("title").GetString());
        Assert.Equal("2% or whole", body.GetProperty("description").GetString());
        Assert.False(body.GetProperty("isCompleted").GetBoolean());
    }

    // Trying to create a todo without a title through the real API should be
    // rejected, and the error should clearly point at the title field.
    [Fact]
    public async Task PostTodos_WithMissingTitle_ReturnsBadRequestWithValidationProblemNamingTitle()
    {
        var request = new { title = "" };

        var response = await _client.PostAsJsonAsync("/todos", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("Title", out _));
    }

    // Trying to create a todo that's already overdue through the real API should
    // be rejected.
    [Fact]
    public async Task PostTodos_WithPastDueDate_ReturnsBadRequest()
    {
        var request = new { title = "Old task", dueDate = "2020-01-01T00:00:00Z" };

        var response = await _client.PostAsJsonAsync("/todos", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // If the client sends a due date with a UTC time zone marker on it, the response
    // should keep that marker when it echoes the due date back.
    [Fact]
    public async Task PostTodos_WithDueDateSentWithUtcOffset_EchoesBackWithOffset()
    {
        var request = new { title = "Buy milk", dueDate = "2026-09-25T14:00:00Z" };

        var response = await _client.PostAsJsonAsync("/todos", request);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var dueDate = body.GetProperty("dueDate").GetString();
        Assert.EndsWith("Z", dueDate);
    }

    // If the client sends a due date with no time zone marker on it (just a plain
    // date), the response shouldn't add one either — this is a known quirk of how
    // dates without a time zone are handled, called out in the feature spec.
    [Fact]
    public async Task PostTodos_WithDueDateSentWithoutOffset_EchoesBackWithoutOffset()
    {
        var request = new { title = "Buy milk", dueDate = "2026-09-25" };

        var response = await _client.PostAsJsonAsync("/todos", request);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var dueDate = body.GetProperty("dueDate").GetString();
        Assert.DoesNotContain("Z", dueDate);
    }
}
