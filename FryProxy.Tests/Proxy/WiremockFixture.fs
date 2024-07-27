namespace FryProxy.Tests.Proxy

open System
open System.IO
open System.Net.Http
open DotNet.Testcontainers.Builders
open NUnit.Framework


[<SetUpFixture>]
type WiremockFixture() =

    [<Literal>]
    static let HTTP_PORT = 8080

    [<Literal>]
    static let HTTPS_PORT = 8443

    static let buildContainer =
        let wiremockFolder =
            Path.Combine(TestContext.CurrentContext.TestDirectory, "wiremock")

        ContainerBuilder()
            .WithImage("wiremock/wiremock:3x")
            .WithCommand("--https-port", $"{HTTPS_PORT}")
            .WithPortBinding(HTTP_PORT)
            .WithPortBinding(HTTPS_PORT)
            .WithResourceMapping(wiremockFolder, "/home/wiremock")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(HTTP_PORT))
            .Build()

    static let lazyContainer = lazy buildContainer

    static member Container = lazyContainer.Value

    static member val HttpClient = new HttpClient(BaseAddress = WiremockFixture.HttpUri)

    static member HttpUri = Uri($"http://localhost:{HTTP_PORT}")

    static member HttpsUri = Uri($"https://localhost:{HTTPS_PORT}")

    [<OneTimeSetUp>]
    static member Start() = lazyContainer.Value.StartAsync()

    [<OneTimeTearDown>]
    static member Stop() = lazyContainer.Value.StartAsync()
