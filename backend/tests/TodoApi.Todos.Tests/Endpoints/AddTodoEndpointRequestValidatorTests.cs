using FluentValidation.TestHelper;
using TodoApi.Todos.Endpoints;

namespace TodoApi.Todos.Tests.Endpoints;

public class AddTodoEndpointRequestValidatorTests
{
    private readonly AddTodoEndpoint.RequestValidator _validator = new();

    // A title that's empty or just spaces should be rejected — a title is required.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrWhitespaceTitle_HasValidationError(string title)
    {
        var request = new AddTodoEndpoint.Request(title, null, null);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    // A title one character over the 200-character limit should be rejected.
    [Fact]
    public void Validate_WithTitleOverMaxLength_HasValidationError()
    {
        var request = new AddTodoEndpoint.Request(new string('a', 201), null, null);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    // A title of exactly 200 characters is still allowed — the limit is inclusive.
    [Fact]
    public void Validate_WithTitleAtMaxLength_HasNoValidationError()
    {
        var request = new AddTodoEndpoint.Request(new string('a', 200), null, null);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    // A description one character over the 2000-character limit should be rejected.
    [Fact]
    public void Validate_WithDescriptionOverMaxLength_HasValidationError()
    {
        var request = new AddTodoEndpoint.Request("Buy milk", new string('a', 2001), null);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    // Leaving out the description entirely is fine — it's an optional field.
    [Fact]
    public void Validate_WithNoDescription_HasNoValidationError()
    {
        var request = new AddTodoEndpoint.Request("Buy milk", null, null);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    // A due date set in the past should be rejected — you can't create a task that's already overdue.
    [Fact]
    public void Validate_WithPastDueDate_HasValidationError()
    {
        var request = new AddTodoEndpoint.Request("Buy milk", null, DateTime.UtcNow.AddDays(-1));

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    // A due date of today should be allowed — today doesn't count as "in the past" yet.
    [Fact]
    public void Validate_WithTodayDueDate_HasNoValidationError()
    {
        var request = new AddTodoEndpoint.Request("Buy milk", null, DateTime.UtcNow.Date);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.DueDate);
    }

    // A due date in the future should be allowed.
    [Fact]
    public void Validate_WithFutureDueDate_HasNoValidationError()
    {
        var request = new AddTodoEndpoint.Request("Buy milk", null, DateTime.UtcNow.AddDays(7));

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.DueDate);
    }

    // Leaving out the due date entirely is fine — it's optional.
    [Fact]
    public void Validate_WithNoDueDate_HasNoValidationError()
    {
        var request = new AddTodoEndpoint.Request("Buy milk", null, null);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.DueDate);
    }

    // A fully filled-out, valid request should pass with no errors at all.
    [Fact]
    public void Validate_WithValidRequestAllFieldsPresent_HasNoValidationErrors()
    {
        var request = new AddTodoEndpoint.Request("Buy milk", "2% or whole", DateTime.UtcNow.AddDays(1));

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
