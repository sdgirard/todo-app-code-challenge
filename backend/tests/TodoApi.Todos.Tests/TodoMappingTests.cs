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
    // client should see, including "last updated" — shared by Add, List, and
    // GetById, so this one mapping backs all three endpoints' responses.
    [Fact]
    public void ToResponse_WithModel_MapsAllFieldsIncludingUpdatedAt()
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

        var response = model.ToResponse();

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
    public void ToResponse_WithNeverUpdatedModel_LeavesUpdatedAtNull()
    {
        var model = new TodoModel
        {
            Id = Guid.NewGuid(),
            Title = "Buy milk",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
        };

        var response = model.ToResponse();

        Assert.Null(response.UpdatedAt);
    }

    // Applying an update request should overwrite every client-writable field —
    // including flipping completion status — and stamp a fresh update time, while
    // never touching the id or when the todo was originally created.
    [Fact]
    public void ApplyTo_WithRequest_OverwritesWritableFieldsAndStampsUpdatedAt()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var model = new TodoModel
        {
            Id = id,
            Title = "Buy milk",
            Description = null,
            DueDate = null,
            IsCompleted = false,
            CreatedAt = createdAt,
            UpdatedAt = null,
        };
        var dueDate = DateTime.UtcNow.AddDays(1);
        var request = new UpdateTodoEndpoint.Request("Buy milk and eggs", "2% or whole", dueDate, true);
        var before = DateTime.UtcNow;

        request.ApplyTo(model);

        var after = DateTime.UtcNow;
        Assert.Equal(id, model.Id);
        Assert.Equal(createdAt, model.CreatedAt);
        Assert.Equal("Buy milk and eggs", model.Title);
        Assert.Equal("2% or whole", model.Description);
        Assert.Equal(dueDate, model.DueDate);
        Assert.True(model.IsCompleted);
        Assert.NotNull(model.UpdatedAt);
        Assert.InRange(model.UpdatedAt!.Value, before, after);
    }

    // A request that omits description/due date should clear them, not leave the
    // existing values in place — this is a full replacement, not a partial update.
    [Fact]
    public void ApplyTo_WithRequestOmittingOptionalFields_ClearsExistingValues()
    {
        var model = new TodoModel
        {
            Id = Guid.NewGuid(),
            Title = "Buy milk",
            Description = "2% or whole",
            DueDate = DateTime.UtcNow.AddDays(1),
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
        };
        var request = new UpdateTodoEndpoint.Request("Buy milk", null, null, false);

        request.ApplyTo(model);

        Assert.Null(model.Description);
        Assert.Null(model.DueDate);
    }
}
