FryProxy
========

Extensible man in the middle HTTP proxy with SSL support. It was written becouse I need a way to monitor and possibly stub request some browser requests in selenium tests.

## Examples:

Setup HTTP proxy:

```csharp
  var httpProxyServer = new HttpProxyServer("localhost", new HttpProxy());
  httpProxyServer.Start().WaitOne();
  
  // do stuff
  
  httpProxyServer.stop();
  
```

Setup SSL proxy:

```csharp
  var certificate = new X509Certificate2("path_to_sertificate", "password");
  var sslProxyServer = new HttpProxyServer("localhost", new SslProxy(certificate));
  
  // do ssl stuff
  
  sslProxyServer.stop();
```

## Extension points
Request are processing in 5 stages:
- receive request from client
- connect to destination server
- send request to server and receive response
- send response back to client
- complete processing and close connections

It is possible to add additional behavior to any stage using delegates:

```csharp
  var httpProxy = new HttpProxy(){
    OnRequestReceived = context => {},
    OnServerConnected = context => {},
    OnResponseReceived = context => {},
    OnResponseSent = context => {},
    OnProcessingComplete = context => {}
  };
```

Context stores request information during processing single request. What you can possibly do with it ?
- modify request and response headers
- modify request and response body
- respond by yourself
- ...or something in between

Please take a look on some [tests](https://github.com/eger-geger/FryProxy/blob/master/FryProxy.Tests/src/InterceptionTests.cs) I have written to show how it can be used!
