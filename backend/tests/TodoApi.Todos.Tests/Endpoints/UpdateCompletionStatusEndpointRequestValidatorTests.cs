using FluentValidation.TestHelper;
using TodoApi.Todos.Endpoints;

namespace TodoApi.Todos.Tests.Endpoints;

public class UpdateCompletionStatusEndpointRequestValidatorTests
{
    private readonly UpdateCompletionStatusEndpoint.RequestValidator _validator = new();

    // A request with IsCompleted set, either way, should pass — there's no
    // meaningful failing case for a non-nullable bool; this documents that the
    // validator is a no-op guard, not a real rejection rule.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validate_WithIsCompletedSet_HasNoValidationErrors(bool isCompleted)
    {
        var request = new UpdateCompletionStatusEndpoint.Request(isCompleted);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
