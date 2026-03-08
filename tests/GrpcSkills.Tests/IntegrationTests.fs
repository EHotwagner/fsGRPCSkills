module GrpcSkills.Tests.IntegrationTests

open Xunit
open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Grpc.Net.Client
open ProtoBuf.Grpc.Server
open ProtoBuf.Grpc.Client
open GrpcSkills.Tests.Contracts
open GrpcSkills.Tests.Services

/// Integration tests using an in-process ASP.NET Core server.

type GrpcTestFixture() =
    let mutable app: WebApplication = Unchecked.defaultof<_>
    let mutable channel: GrpcChannel = Unchecked.defaultof<_>

    member _.Channel = channel

    member _.Start() = task {
        Setup.ensureRegistered ()

        let builder = WebApplication.CreateBuilder()
        builder.Services.AddCodeFirstGrpc() |> ignore

        builder.WebHost.ConfigureKestrel(fun options ->
            options.Listen(System.Net.IPAddress.Loopback, 0, fun listenOptions ->
                listenOptions.Protocols <-
                    Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2
            )
        ) |> ignore

        app <- builder.Build()
        app.MapGrpcService<GreeterService>() |> ignore
        app.MapGrpcService<SearchService>() |> ignore
        app.MapGrpcService<OrderService>() |> ignore
        app.MapGrpcService<TickerService>() |> ignore
        app.MapGrpcService<ChatService>() |> ignore

        do! app.StartAsync()

        let address = app.Urls |> Seq.head
        channel <- GrpcChannel.ForAddress(address)
    }

    member _.Stop() = task {
        if channel <> Unchecked.defaultof<_> then
            channel.Dispose()
        if app <> Unchecked.defaultof<_> then
            do! app.StopAsync()
            do! app.DisposeAsync()
    }

// ── Greeter Integration Tests ──

[<Fact>]
let ``Integration: Greeter SayHello works end-to-end`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<IGreeterService>()
        let! reply = client.SayHello({ Name = "Integration" }, ProtoBuf.Grpc.CallContext.Default)
        Assert.Equal("Hello, Integration!", reply.Message)
    finally
        fixture.Stop().Wait()
}

[<Fact>]
let ``Integration: Greeter handles special characters`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<IGreeterService>()
        let! reply = client.SayHello({ Name = "O'Brien & \"Friends\"" }, ProtoBuf.Grpc.CallContext.Default)
        Assert.Equal("Hello, O'Brien & \"Friends\"!", reply.Message)
    finally
        fixture.Stop().Wait()
}

// ── Search Integration Tests ──

[<Fact>]
let ``Integration: Search returns results`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<ISearchService>()
        let request = { Query = "grpc"; MaxResults = None; Category = None }
        let! reply = client.Search(request, ProtoBuf.Grpc.CallContext.Default)
        Assert.True(reply.Results.Length > 0)
        Assert.True(reply.TotalCount > 0)
    finally
        fixture.Stop().Wait()
}

[<Fact>]
let ``Integration: Search with MaxResults limits results`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<ISearchService>()
        let request = { Query = "f#"; MaxResults = Some 1; Category = None }
        let! reply = client.Search(request, ProtoBuf.Grpc.CallContext.Default)
        Assert.True(reply.Results.Length <= 1)
    finally
        fixture.Stop().Wait()
}

[<Fact>]
let ``Integration: Search with no matches returns empty`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<ISearchService>()
        let request = { Query = "zzz_no_match"; MaxResults = None; Category = None }
        let! reply = client.Search(request, ProtoBuf.Grpc.CallContext.Default)
        Assert.Empty(reply.Results)
    finally
        fixture.Stop().Wait()
}

// ── Order Integration Tests ──

[<Fact>]
let ``Integration: PlaceOrder succeeds with valid order`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<IOrderService>()
        let order =
            { Id = "int-001"
              Lines = [| { ProductId = "P1"; Quantity = 2; UnitPrice = 10.0m } |]
              Tags = Dictionary<string,string>() }
        let! result = client.PlaceOrder(order, ProtoBuf.Grpc.CallContext.Default)
        Assert.True(OrderResult.isOk result)
        Assert.Equal(20.0m, result.Confirmation.Total)
    finally
        fixture.Stop().Wait()
}

[<Fact>]
let ``Integration: PlaceOrder fails with empty lines`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<IOrderService>()
        let order = { Id = "int-002"; Lines = [||]; Tags = Dictionary<string,string>() }
        let! result = client.PlaceOrder(order, ProtoBuf.Grpc.CallContext.Default)
        Assert.False(OrderResult.isOk result)
        Assert.Equal(400, result.ErrorCode)
    finally
        fixture.Stop().Wait()
}

// ── Ticker Streaming Integration Tests ──

[<Fact>]
let ``Integration: Ticker streams tick data`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<ITickerService>()
        let stream = client.Subscribe({ Symbol = "GOOG" }, ProtoBuf.Grpc.CallContext.Default)
        let mutable count = 0
        let enumerator = stream.GetAsyncEnumerator(CancellationToken.None)
        try
            while! enumerator.MoveNextAsync() do
                Assert.Equal("GOOG", enumerator.Current.Symbol)
                count <- count + 1
        finally
            enumerator.DisposeAsync().AsTask().Wait()
        Assert.Equal(5, count)
    finally
        fixture.Stop().Wait()
}

// ── Chat Bidirectional Streaming Integration Tests ──

[<Fact>]
let ``Integration: Chat echoes messages`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<IChatService>()
        let messages =
            AsyncEnumerableWrapper(
                [ { User = "test"; Text = "ping" }
                  { User = "test"; Text = "pong" } ])
            :> IAsyncEnumerable<ChatMessage>

        let responses = client.Chat(messages, ProtoBuf.Grpc.CallContext.Default)
        let mutable count = 0
        let enumerator = responses.GetAsyncEnumerator(CancellationToken.None)
        try
            while! enumerator.MoveNextAsync() do
                Assert.Equal("Bot", enumerator.Current.User)
                Assert.StartsWith("Echo: ", enumerator.Current.Text)
                count <- count + 1
        finally
            enumerator.DisposeAsync().AsTask().Wait()
        Assert.Equal(2, count)
    finally
        fixture.Stop().Wait()
}

// ── Multiple Calls ──

[<Fact>]
let ``Integration: Multiple sequential calls on same channel`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<IGreeterService>()

        for i in 1..5 do
            let! reply = client.SayHello({ Name = $"Call{i}" }, ProtoBuf.Grpc.CallContext.Default)
            Assert.Equal($"Hello, Call{i}!", reply.Message)
    finally
        fixture.Stop().Wait()
}

[<Fact>]
let ``Integration: Concurrent calls on same channel`` () = task {
    let fixture = new GrpcTestFixture()
    try
        do! fixture.Start()
        let client = fixture.Channel.CreateGrpcService<IGreeterService>()

        let tasks =
            [| for i in 1..10 ->
                task {
                    let! reply = client.SayHello({ Name = $"Concurrent{i}" }, ProtoBuf.Grpc.CallContext.Default)
                    return reply.Message
                } |]

        let! results = Task.WhenAll(tasks)
        Assert.Equal(10, results.Length)
        for i in 1..10 do
            Assert.Contains($"Hello, Concurrent{i}!", results)
    finally
        fixture.Stop().Wait()
}
