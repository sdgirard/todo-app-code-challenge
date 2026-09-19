using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TodoApi.Todos.Endpoints;

namespace TodoApi.Todos.Tests.Endpoints;

public class ValidationFilterTests
{
    // This is a made-up request type, not the real AddTodo one. The filter is meant
    // to work for any endpoint's request, so we're testing it on its own here rather
    // than tying the test to one specific endpoint's rules.
    private sealed record Request(string Title);

    private sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator() => RuleFor(x => x.Title).NotEmpty();
    }

    // When the request is invalid, the endpoint's actual code should never run at
    // all — the filter should stop it early and hand back a validation error instead.
    [Fact]
    public async Task InvokeAsync_WithInvalidRequest_ShortCircuitsToValidationProblem()
    {
        var filter = new ValidationFilter<Request>(new RequestValidator());
        var context = EndpointFilterInvocationContext.Create(new DefaultHttpContext(), new Request(""));
        var nextCalled = false;

        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(TypedResults.Ok());
        });

        Assert.False(nextCalled);
        Assert.IsType<ValidationProblem>(result);
    }

    // When the request is valid, the filter should just let the endpoint's actual
    // code run and pass back whatever it returns, untouched.
    [Fact]
    public async Task InvokeAsync_WithValidRequest_CallsNextAndReturnsItsResult()
    {
        var filter = new ValidationFilter<Request>(new RequestValidator());
        var context = EndpointFilterInvocationContext.Create(new DefaultHttpContext(), new Request("Buy milk"));
        var expected = TypedResults.Ok();

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(expected));

        Assert.Same(expected, result);
    }
}
