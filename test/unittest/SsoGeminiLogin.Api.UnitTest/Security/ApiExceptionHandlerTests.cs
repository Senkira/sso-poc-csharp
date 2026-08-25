using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SsoGeminiLogin.Api.Security;

namespace SsoGeminiLogin.Api.UnitTest.Security;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsyncWhenResponseStartedDoesNotInvokeProblemWriter()
    {
        var problemDetailsService = new RecordingProblemDetailsService();
        var handler = CreateHandler(problemDetailsService);
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedHttpResponseFeature());

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("sensitive-detail"),
            CancellationToken.None);

        Assert.False(handled);
        Assert.Null(problemDetailsService.Context);
    }

    [Fact]
    public async Task TryHandleAsyncWhenProblemWriterFailsPropagatesFailure()
    {
        var handler = CreateHandler(new ThrowingProblemDetailsService());
        var context = new DefaultHttpContext();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.TryHandleAsync(
                context,
                new IOException("sensitive-detail"),
                CancellationToken.None));

        Assert.Equal("simulated-problem-writer-failure", exception.Message);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsyncWritesSanitizedProblemDetailsWithTraceIdentifier()
    {
        var problemDetailsService = new RecordingProblemDetailsService();
        var handler = CreateHandler(problemDetailsService);
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-test-123"
        };
        context.Request.Path = "/api/v1/account-mappings/current";

        var handled = await handler.TryHandleAsync(
            context,
            new IOException("sensitive-detail"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.NotNull(problemDetailsService.Context);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("An unexpected broker error occurred.", problemDetailsService.Context.ProblemDetails.Title);
        Assert.Equal("/api/v1/account-mappings/current", problemDetailsService.Context.ProblemDetails.Instance);
        Assert.Equal("trace-test-123", problemDetailsService.Context.ProblemDetails.Extensions["traceId"]);
        Assert.DoesNotContain(
            "sensitive-detail",
            problemDetailsService.Context.ProblemDetails.Title,
            StringComparison.Ordinal);
    }

    private static ApiExceptionHandler CreateHandler(IProblemDetailsService problemDetailsService) =>
        new(problemDetailsService, NullLogger<ApiExceptionHandler>.Instance);

    private sealed class RecordingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Context { get; private set; }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Context = context;
            return ValueTask.FromResult(true);
        }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Context = context;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingProblemDetailsService : IProblemDetailsService
    {
        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) =>
            throw new InvalidOperationException("simulated-problem-writer-failure");

        public ValueTask WriteAsync(ProblemDetailsContext context) =>
            throw new InvalidOperationException("simulated-problem-writer-failure");
    }

    private sealed class StartedHttpResponseFeature : IHttpResponseFeature
    {
        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted => true;

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public string? ReasonPhrase { get; set; }

        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }
    }
}
