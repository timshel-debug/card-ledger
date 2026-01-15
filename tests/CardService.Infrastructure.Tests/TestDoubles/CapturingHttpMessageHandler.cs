namespace CardService.Infrastructure.Tests.TestDoubles;

public class CapturingHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    
    public HttpRequestMessage? LastRequest { get; private set; }
    public List<HttpRequestMessage> AllRequests { get; } = new();

    public void AddResponse(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
    }

    public void AddResponse(string content, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
        AddResponse(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        AllRequests.Add(request);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No response configured for this request. Call AddResponse() first.");
        }

        var response = _responses.Dequeue();
        return Task.FromResult(response);
    }
}
