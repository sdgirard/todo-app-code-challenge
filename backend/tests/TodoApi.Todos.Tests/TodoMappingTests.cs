using TodoApi.Todos.Endpoints;
using TodoApi.Todos.Models;

namespace TodoApi.Todos.Tests;

public class TodoMappingTests
{
    // Turning a request into a new todo should copy over what the client sent,
    // and also fill in the things only the server should set (a new id, not
    // completed yet, when it was created, and no update time yet).
    [Fact]
    public void ToModel_WithRequest_GeneratesIdAndDefaultsCreateFields()
    {
        var dueDate = DateTime.UtcNow.AddDays(1);
        var request = new AddTodoEndpoint.Request("Buy milk", "2% or whole", dueDate);
        var before = DateTime.UtcNow;

        var model = request.ToModel();

        var after = DateTime.UtcNow;
        Assert.NotEqual(Guid.Empty, model.Id);
        Assert.Equal("Buy milk", model.Title);
        Assert.Equal("2% or whole", model.Description);
        Assert.Equal(dueDate, model.DueDate);
        Assert.False(model.IsCompleted);
        Assert.InRange(model.CreatedAt, before, after);
        Assert.Null(model.UpdatedAt);
    }

    // Two different todos should never end up with the same id.
    [Fact]
    public void ToModel_CalledTwice_GeneratesDistinctIds()
    {
        var request = new AddTodoEndpoint.Request("Buy milk", null, null);

        var first = request.ToModel();
        var second = request.ToModel();

        Assert.NotEqual(first.Id, second.Id);
    }

    // Turning a saved todo into a response should carry over every field the
    // client should see, including "last updated" (null for a brand new todo,
    // but the field itself must still be present for clients that always read it).
    [Fact]
    public void ToResponse_WithModel_MapsAllFields()
    {
        var model = new TodoModel
        {
            Id = Guid.NewGuid(),
            Title = "Buy milk",
            Description = "2% or whole",
            DueDate = DateTime.UtcNow.AddDays(1),
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var response = model.ToResponse();

        Assert.Equal(model.Id, response.Id);
        Assert.Equal(model.Title, response.Title);
        Assert.Equal(model.Description, response.Description);
        Assert.Equal(model.DueDate, response.DueDate);
        Assert.Equal(model.IsCompleted, response.IsCompleted);
        Assert.Equal(model.CreatedAt, response.CreatedAt);
        Assert.Equal(model.UpdatedAt, response.UpdatedAt);
    }

    // Turning a saved todo into a list item should carry over every field the client
    // should see — including the "last updated" time, which the create response drops
    // but a list of possibly-edited todos genuinely needs.
    [Fact]
    public void ToListResponse_WithModel_MapsAllFieldsIncludingUpdatedAt()
    {
        var model = new TodoModel
        {
            Id = Guid.NewGuid(),
            Title = "Buy milk",
            Description = "2% or whole",
            DueDate = DateTime.UtcNow.AddDays(1),
            IsCompleted = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow,
        };

        var response = model.ToListResponse();

        Assert.Equal(model.Id, response.Id);
        Assert.Equal(model.Title, response.Title);
        Assert.Equal(model.Description, response.Description);
        Assert.Equal(model.DueDate, response.DueDate);
        Assert.Equal(model.IsCompleted, response.IsCompleted);
        Assert.Equal(model.CreatedAt, response.CreatedAt);
        Assert.Equal(model.UpdatedAt, response.UpdatedAt);
    }

    // A todo that's never been edited should report no update time at all.
    [Fact]
    public void ToListResponse_WithNeverUpdatedModel_LeavesUpdatedAtNull()
    {
        var model = new TodoModel
        {
            Id = Guid.NewGuid(),
            Title = "Buy milk",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
        };

        var response = model.ToListResponse();

        Assert.Null(response.UpdatedAt);
    }
}
