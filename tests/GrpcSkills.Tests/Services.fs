namespace GrpcSkills.Tests

open System.Threading.Tasks
open System.Collections.Generic
open System.Threading
open ProtoBuf.Grpc
open GrpcSkills.Tests.Contracts

/// Service implementations for testing server patterns.
module Services =

    /// Simple greeter service (unary RPC).
    type GreeterService() =
        interface IGreeterService with
            member _.SayHello(request, _context) =
                let reply = { Message = $"Hello, {request.Name}!" }
                ValueTask<GreetReply>(reply)

    /// Search service with optional fields.
    type SearchService() =
        let items =
            [| { Id = "1"; Title = "F# gRPC Guide"; Score = 0.95 }
               { Id = "2"; Title = "Protobuf for F#"; Score = 0.88 }
               { Id = "3"; Title = "ASP.NET Core gRPC"; Score = 0.82 }
               { Id = "4"; Title = "F# Web Development"; Score = 0.75 }
               { Id = "5"; Title = "gRPC Streaming Patterns"; Score = 0.70 } |]

        interface ISearchService with
            member _.Search(request, _context) =
                let filtered =
                    items
                    |> Array.filter (fun i ->
                        i.Title.ToLowerInvariant().Contains(request.Query.ToLowerInvariant()))

                let limited =
                    match request.MaxResults with
                    | Some max -> filtered |> Array.truncate max
                    | None -> filtered

                let reply =
                    { Results = limited
                      TotalCount = filtered.Length }

                ValueTask<SearchReply>(reply)

    /// Order service with collections and result pattern.
    type OrderService() =
        interface IOrderService with
            member _.PlaceOrder(request, _context) =
                if request.Lines.Length = 0 then
                    let result = OrderResult.error 400 "Order must have at least one line"
                    ValueTask<OrderResult>(result)
                else
                    let total =
                        request.Lines
                        |> Array.sumBy (fun l -> l.UnitPrice * decimal l.Quantity)
                    let confirmation =
                        { OrderId = System.Guid.NewGuid().ToString("N")
                          Total = total }
                    ValueTask<OrderResult>(OrderResult.ok confirmation)

    /// Helper to create IAsyncEnumerable from a sequence.
    type AsyncEnumerableWrapper<'T>(items: 'T seq) =
        interface IAsyncEnumerable<'T> with
            member _.GetAsyncEnumerator(_ct) =
                let enumerator = items.GetEnumerator()
                { new IAsyncEnumerator<'T> with
                    member _.Current = enumerator.Current
                    member _.MoveNextAsync() = ValueTask<bool>(enumerator.MoveNext())
                    member _.DisposeAsync() =
                        enumerator.Dispose()
                        ValueTask.CompletedTask }

    /// Ticker service with server streaming.
    type TickerService() =
        interface ITickerService with
            member _.Subscribe(request, _context) =
                let ticks =
                    [ for i in 1..5 ->
                        { Symbol = request.Symbol
                          Price = 100.0 + float i
                          Volume = 1000 * i } ]
                AsyncEnumerableWrapper(ticks) :> IAsyncEnumerable<TickData>

    /// Chat service with bidirectional streaming.
    type ChatService() =
        interface IChatService with
            member _.Chat(messages, _context) =
                let collectAndEcho () =
                    let results = System.Collections.Generic.List<ChatMessage>()
                    let enumerator = messages.GetAsyncEnumerator(CancellationToken.None)
                    let rec loop () = task {
                        let! hasNext = enumerator.MoveNextAsync()
                        if hasNext then
                            let msg = enumerator.Current
                            results.Add({ User = "Bot"; Text = $"Echo: {msg.Text}" })
                            return! loop ()
                    }
                    task {
                        try
                            do! loop ()
                        finally
                            enumerator.DisposeAsync().AsTask().Wait()
                        return results :> seq<ChatMessage>
                    }
                let items = collectAndEcho().Result
                AsyncEnumerableWrapper(items) :> IAsyncEnumerable<ChatMessage>
