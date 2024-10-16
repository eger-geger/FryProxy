using System.Net.Security;
using System.Security.Authentication;
using FryProxy;
using FryProxy.Http;
using FryProxy.Pipeline;

using var proxy = CreateProxy();
proxy.Start();

Console.WriteLine($"Started at... {proxy.Endpoint}");

Thread.Sleep(Timeout.Infinite);

return;

HttpProxy<DefaultContext> CreateProxy()
{
    var tunnelFactory = TransparentTunnel.NaiveFactoryWithSelfSignedCertificate<DefaultContext>();
    return new HttpProxy<DefaultContext>(LogRequestAndResponse, new Settings(), tunnelFactory);
}

async ValueTask<Tuple<Message<StatusLine>, DefaultContext>> LogRequestAndResponse(
    Message<RequestLine> request, RequestHandler<DefaultContext> next)
{
    Console.WriteLine($"->{request}");

    var result = await next(request);

    Console.WriteLine($"<-{result.Item1}");

    return result;
}