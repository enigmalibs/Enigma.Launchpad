using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Answers HTTP requests from a table of addresses, recording each request; anything not in the table is a 404.
/// </summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<Uri, Func<HttpRequestMessage, HttpResponseMessage>> _answers = new();

    public ConcurrentQueue<(Uri Address, string Accept, string UserAgent)> Requests { get; } = new();

    public void Answer(string address, Func<HttpRequestMessage, HttpResponseMessage> answer)
        => _answers[new Uri(address)] = answer;

    public void Answer(string address, byte[] body, string mediaType, HttpStatusCode status = HttpStatusCode.OK)
        => Answer(address, _ => Response(body, mediaType, status));

    public void Html(string address, string html) => Answer(address, System.Text.Encoding.UTF8.GetBytes(html), "text/html");

    public void Fail(string address, Exception failure) => Answer(address, _ => throw failure);

    public static HttpResponseMessage Response(byte[] body, string mediaType, HttpStatusCode status = HttpStatusCode.OK)
    {
        ByteArrayContent content = new(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(status) { Content = content };
    }

    /// <summary>
    /// A body whose length is not announced, as a streamed or chunked response's is not.
    /// </summary>
    public static HttpResponseMessage UnannouncedResponse(byte[] body, string mediaType)
    {
        StreamContent content = new(new UnseekableStream(body));
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri address = request.RequestUri ?? throw new InvalidOperationException("A request without an address.");
        Requests.Enqueue((address, request.Headers.Accept.ToString(), request.Headers.UserAgent.ToString()));

        HttpResponseMessage response = _answers.TryGetValue(address, out Func<HttpRequestMessage, HttpResponseMessage>? answer)
            ? answer(request)
            : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new ByteArrayContent([]) };

        // As the real handler does: it is how a caller learns where redirects ended.
        response.RequestMessage ??= request;
        return Task.FromResult(response);
    }

    private sealed class UnseekableStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override bool CanSeek => false;
    }
}

/// <summary>
/// Bytes that start like the image formats a fetcher accepts.
/// </summary>
internal static class ImageBytes
{
    public static byte[] Png(params byte[] rest) => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. rest];

    public static byte[] Ico(params byte[] rest) => [0x00, 0x00, 0x01, 0x00, .. rest];
}
