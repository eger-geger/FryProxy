namespace FryProxy.Tests.Proxy

open System
open System.IO
open DotNet.Testcontainers.Builders
open NUnit.Framework


[<SetUpFixture>]
type WiremockFixture() =

    [<Literal>]
    static let WIREMOCK_PORT = 8080

    static let buildContainer =
        let wiremockFolder =
            Path.Combine(TestContext.CurrentContext.TestDirectory, "wiremock")

        ContainerBuilder()
            .WithImage("wiremock/wiremock:3x")
            .WithPortBinding(WIREMOCK_PORT)
            .WithResourceMapping(wiremockFolder, "/home/wiremock")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(WIREMOCK_PORT))
            .Build()

    static let lazyContainer = lazy buildContainer

    static member Container = lazyContainer.Value

    static member Uri = Uri($"http://{WiremockFixture.Container.Hostname}:{WIREMOCK_PORT}")

    [<OneTimeSetUp>]
    static member Start() = lazyContainer.Value.StartAsync()

    [<OneTimeTearDown>]
    static member Stop() = lazyContainer.Value.StartAsync()
