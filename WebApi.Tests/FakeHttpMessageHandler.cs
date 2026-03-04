// A minimal fake HttpMessageHandler that returns a canned response.
// No third-party mocking libraries needed — just override SendAsync.
//
// This is the key seam that makes both named-client and typed-client tests
// possible: IHttpClientFactory and AddHttpClient<T> both accept a custom
// primary handler via ConfigurePrimaryHttpMessageHandler().

namespace WebApi.Tests;

public class FakeHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    : HttpMessageHandler
{
    // Track calls so tests can assert the right path was requested.
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody)
        };

        return Task.FromResult(response);
    }
}
