using FryProxy;
using FryProxy.Http;
using FryProxy.Pipeline;

using var proxy = new HttpProxy<DefaultContext>(
    LogRequestAndResponse, new Settings(), OpaqueTunnel.Factory<DefaultContext>()
);

proxy.Start();

Console.WriteLine($"Started at... {proxy.Endpoint}");

Thread.Sleep(Timeout.Infinite);

return;

async ValueTask<Tuple<Message<StatusLine>, DefaultContext>> LogRequestAndResponse(
    Message<RequestLine> request, RequestHandler<DefaultContext> next
)
{
    Console.WriteLine($"->{request}");

    var result = await next(request);

    Console.WriteLine($"<-{result.Item1}");

    return result;
}