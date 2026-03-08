module GrpcSkills.Tests.ServiceTests

open Xunit
open System.Threading.Tasks
open System.Collections.Generic
open GrpcSkills.Tests.Contracts
open GrpcSkills.Tests.Services

// ── Greeter Service Tests ──

[<Fact>]
let ``GreeterService returns greeting with name`` () = task {
    let service = GreeterService() :> IGreeterService
    let! reply = service.SayHello({ Name = "World" }, ProtoBuf.Grpc.CallContext.Default)
    Assert.Equal("Hello, World!", reply.Message)
}

[<Fact>]
let ``GreeterService handles empty name`` () = task {
    let service = GreeterService() :> IGreeterService
    let! reply = service.SayHello({ Name = "" }, ProtoBuf.Grpc.CallContext.Default)
    Assert.Equal("Hello, !", reply.Message)
}

[<Fact>]
let ``GreeterService handles unicode name`` () = task {
    let service = GreeterService() :> IGreeterService
    let! reply = service.SayHello({ Name = "\u4E16\u754C" }, ProtoBuf.Grpc.CallContext.Default)
    Assert.Equal("Hello, \u4E16\u754C!", reply.Message)
}

// ── Search Service Tests ──

[<Fact>]
let ``SearchService returns matching results`` () = task {
    let service = SearchService() :> ISearchService
    let request = { Query = "grpc"; MaxResults = None; Category = None }
    let! reply = service.Search(request, ProtoBuf.Grpc.CallContext.Default)
    Assert.True(reply.Results.Length > 0)
    Assert.True(reply.Results |> Array.forall (fun r ->
        r.Title.ToLowerInvariant().Contains("grpc")))
}

[<Fact>]
let ``SearchService respects MaxResults`` () = task {
    let service = SearchService() :> ISearchService
    let request = { Query = "f#"; MaxResults = Some 1; Category = None }
    let! reply = service.Search(request, ProtoBuf.Grpc.CallContext.Default)
    Assert.True(reply.Results.Length <= 1)
}

[<Fact>]
let ``SearchService returns empty for no match`` () = task {
    let service = SearchService() :> ISearchService
    let request = { Query = "nonexistent_xyz"; MaxResults = None; Category = None }
    let! reply = service.Search(request, ProtoBuf.Grpc.CallContext.Default)
    Assert.Empty(reply.Results)
    Assert.Equal(0, reply.TotalCount)
}

[<Fact>]
let ``SearchService with MaxResults larger than results`` () = task {
    let service = SearchService() :> ISearchService
    let request = { Query = "grpc"; MaxResults = Some 100; Category = None }
    let! reply = service.Search(request, ProtoBuf.Grpc.CallContext.Default)
    Assert.True(reply.Results.Length <= 100)
    Assert.Equal(reply.Results.Length, reply.TotalCount)
}

// ── Order Service Tests ──

[<Fact>]
let ``OrderService succeeds with valid order`` () = task {
    let service = OrderService() :> IOrderService
    let tags = Dictionary<string,string>()
    tags.["source"] <- "test"
    let order =
        { Id = "test-001"
          Lines =
            [| { ProductId = "P1"; Quantity = 2; UnitPrice = 10.0m }
               { ProductId = "P2"; Quantity = 1; UnitPrice = 25.0m } |]
          Tags = tags }
    let! result = service.PlaceOrder(order, ProtoBuf.Grpc.CallContext.Default)
    Assert.True(OrderResult.isOk result)
    Assert.Equal(45.0m, result.Confirmation.Total)
    Assert.False(System.String.IsNullOrEmpty(result.Confirmation.OrderId))
}

[<Fact>]
let ``OrderService fails with empty order`` () = task {
    let service = OrderService() :> IOrderService
    let order = { Id = "test-002"; Lines = [||]; Tags = Dictionary<string,string>() }
    let! result = service.PlaceOrder(order, ProtoBuf.Grpc.CallContext.Default)
    Assert.False(OrderResult.isOk result)
    Assert.Equal(400, result.ErrorCode)
    Assert.Contains("at least one line", result.Error)
}

[<Fact>]
let ``OrderService calculates total correctly for single item`` () = task {
    let service = OrderService() :> IOrderService
    let order =
        { Id = "test-003"
          Lines = [| { ProductId = "P1"; Quantity = 3; UnitPrice = 15.50m } |]
          Tags = Dictionary<string,string>() }
    let! result = service.PlaceOrder(order, ProtoBuf.Grpc.CallContext.Default)
    Assert.Equal(46.50m, result.Confirmation.Total)
}

// ── Ticker Service Tests ──

[<Fact>]
let ``TickerService returns stream of ticks`` () = task {
    let service = TickerService() :> ITickerService
    let stream = service.Subscribe({ Symbol = "MSFT" }, ProtoBuf.Grpc.CallContext.Default)
    let mutable count = 0
    let enumerator = stream.GetAsyncEnumerator(System.Threading.CancellationToken.None)
    try
        while! enumerator.MoveNextAsync() do
            let tick = enumerator.Current
            Assert.Equal("MSFT", tick.Symbol)
            Assert.True(tick.Price > 0.0)
            Assert.True(tick.Volume > 0)
            count <- count + 1
    finally
        enumerator.DisposeAsync().AsTask().Wait()

    Assert.Equal(5, count)
}

[<Fact>]
let ``TickerService returns increasing prices`` () = task {
    let service = TickerService() :> ITickerService
    let stream = service.Subscribe({ Symbol = "AAPL" }, ProtoBuf.Grpc.CallContext.Default)
    let mutable lastPrice = 0.0
    let enumerator = stream.GetAsyncEnumerator(System.Threading.CancellationToken.None)
    try
        while! enumerator.MoveNextAsync() do
            Assert.True(enumerator.Current.Price > lastPrice)
            lastPrice <- enumerator.Current.Price
    finally
        enumerator.DisposeAsync().AsTask().Wait()
}

// ── Chat Service Tests ──

[<Fact>]
let ``ChatService echoes messages`` () = task {
    let service = ChatService() :> IChatService
    let incoming =
        AsyncEnumerableWrapper(
            [ { User = "alice"; Text = "Hello" }
              { User = "alice"; Text = "World" } ])
        :> System.Collections.Generic.IAsyncEnumerable<ChatMessage>

    let responses = service.Chat(incoming, ProtoBuf.Grpc.CallContext.Default)
    let mutable count = 0
    let enumerator = responses.GetAsyncEnumerator(System.Threading.CancellationToken.None)
    try
        while! enumerator.MoveNextAsync() do
            let msg = enumerator.Current
            Assert.Equal("Bot", msg.User)
            Assert.StartsWith("Echo: ", msg.Text)
            count <- count + 1
    finally
        enumerator.DisposeAsync().AsTask().Wait()

    Assert.Equal(2, count)
}

// ── OrderResult Pattern Tests ──

[<Fact>]
let ``OrderResult.ok creates success result`` () =
    let result = OrderResult.ok { OrderId = "x"; Total = 0m }
    Assert.True(OrderResult.isOk result)
    Assert.Null(result.Error)
    Assert.Equal(0, result.ErrorCode)

[<Fact>]
let ``OrderResult.error creates error result`` () =
    let result = OrderResult.error 404 "Not found"
    Assert.False(OrderResult.isOk result)
    Assert.Equal("Not found", result.Error)
    Assert.Equal(404, result.ErrorCode)
