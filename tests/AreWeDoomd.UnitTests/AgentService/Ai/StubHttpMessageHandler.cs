namespace AreWeDoomd.UnitTests.AgentService.Ai;

/// <summary>
/// Test double that lets a test drive exactly what an <see cref="HttpClient"/>
/// sees on the wire: either a canned response or a thrown transport exception.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

    public StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    /// <summary>Always returns the same single <paramref name="response"/> instance (single-call scenarios only).</summary>
    public static StubHttpMessageHandler RespondWith(HttpResponseMessage response) =>
        new((_, _) => Task.FromResult(response));

    /// <summary>Calls <paramref name="factory"/> on every request so each call gets a fresh response.</summary>
    public static StubHttpMessageHandler AlwaysRespondWith(Func<HttpResponseMessage> factory) =>
        new((_, _) => Task.FromResult(factory()));

    public static StubHttpMessageHandler Throw(Exception exception) =>
        new((_, _) => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        _responder(request, cancellationToken);
}
