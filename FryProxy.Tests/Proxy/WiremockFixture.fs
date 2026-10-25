namespace FryProxy.Tests.Proxy

open System
open System.IO
open System.Net
open System.Net.Http
open DotNet.Testcontainers.Builders
open DotNet.Testcontainers.Configurations
open NUnit.Framework


[<SetUpFixture>]
type WiremockFixture() =

    [<Literal>]
    static let HTTP_PORT = 8080

    [<Literal>]
    static let HTTPS_PORT = 8443

    static let buildContainer () =
        let wiremockFolder =
            Path.Combine(TestContext.CurrentContext.TestDirectory, "wiremock")

        let getExampleOrg (httpWait: HttpWaitStrategy) =
            httpWait
                .ForPort(uint16 HTTP_PORT)
                .WithMethod(HttpMethod.Get)
                .ForPath("/example.org")
                .ForStatusCode(HttpStatusCode.OK)

        ContainerBuilder("wiremock/wiremock:3x")
            .WithCommand("--https-port", $"{HTTPS_PORT}")
            .WithPortBinding(HTTP_PORT)
            .WithPortBinding(HTTPS_PORT)
            .WithResourceMapping(wiremockFolder, "/home/wiremock")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(HTTP_PORT))
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(getExampleOrg))
            .Build()

    static let lazyContainer = lazy (buildContainer ())

    static member Container = lazyContainer.Value

    static member val HttpClient = new HttpClient(BaseAddress = WiremockFixture.HttpUri)

    static member HttpUri = Uri($"http://localhost:{HTTP_PORT}")

    static member HttpsUri = Uri($"https://localhost:{HTTPS_PORT}")

    [<OneTimeSetUp>]
    static member Start() =
        let ct = TestContext.CurrentContext.CancellationToken

        task { do! lazyContainer.Value.StartAsync(ct) }

    [<OneTimeTearDown>]
    static member Stop() = lazyContainer.Value.StopAsync()
