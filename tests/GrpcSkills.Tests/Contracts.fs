namespace GrpcSkills.Tests

open ProtoBuf
open System.ServiceModel
open System.Threading.Tasks
open ProtoBuf.Grpc
open System.Collections.Generic

/// Shared data contracts for testing all gRPC skill patterns.
/// Note: protobuf-net requires mutable collections (array, Dictionary)
/// rather than F# immutable collections (list, Map) for repeated/map fields.
module Contracts =

    // ── Basic Request/Response ──

    [<ProtoContract>]
    type GreetRequest =
        { [<ProtoMember(1)>] Name: string }

    [<ProtoContract>]
    type GreetReply =
        { [<ProtoMember(1)>] Message: string }

    // ── Optional Fields ──

    [<ProtoContract>]
    type SearchRequest =
        { [<ProtoMember(1)>] Query: string
          [<ProtoMember(2)>] MaxResults: int option
          [<ProtoMember(3)>] Category: string option }

    [<ProtoContract>]
    type SearchResult =
        { [<ProtoMember(1)>] Id: string
          [<ProtoMember(2)>] Title: string
          [<ProtoMember(3)>] Score: float }

    [<ProtoContract>]
    type SearchReply =
        { [<ProtoMember(1)>] Results: SearchResult array
          [<ProtoMember(2)>] TotalCount: int }

    // ── Collections ──

    [<ProtoContract>]
    type OrderLine =
        { [<ProtoMember(1)>] ProductId: string
          [<ProtoMember(2)>] Quantity: int
          [<ProtoMember(3)>] UnitPrice: decimal }

    [<ProtoContract>]
    type Order =
        { [<ProtoMember(1)>] Id: string
          [<ProtoMember(2)>] Lines: OrderLine array
          [<ProtoMember(3)>] Tags: Dictionary<string, string> }

    [<ProtoContract>]
    type OrderConfirmation =
        { [<ProtoMember(1)>] OrderId: string
          [<ProtoMember(2)>] Total: decimal }

    // ── Nested Records ──

    [<ProtoContract>]
    type Address =
        { [<ProtoMember(1)>] Street: string
          [<ProtoMember(2)>] City: string
          [<ProtoMember(3)>] PostCode: string }

    [<ProtoContract>]
    type Person =
        { [<ProtoMember(1)>] Name: string
          [<ProtoMember(2)>] Age: int
          [<ProtoMember(3)>] Address: Address
          [<ProtoMember(4)>] Email: string option }

    // ── Streaming Types ──

    [<ProtoContract>]
    type TickData =
        { [<ProtoMember(1)>] Symbol: string
          [<ProtoMember(2)>] Price: float
          [<ProtoMember(3)>] Volume: int }

    [<ProtoContract>]
    type SubscribeRequest =
        { [<ProtoMember(1)>] Symbol: string }

    [<ProtoContract>]
    type ChatMessage =
        { [<ProtoMember(1)>] User: string
          [<ProtoMember(2)>] Text: string }

    [<ProtoContract>]
    type DataChunk =
        { [<ProtoMember(1)>] Data: string
          [<ProtoMember(2)>] Index: int }

    [<ProtoContract>]
    type UploadResult =
        { [<ProtoMember(1)>] ChunksReceived: int }

    // ── Service Result Pattern ──
    // Note: Generic records with option fields require careful registration.
    // For simplicity in tests, use a non-generic version.

    [<ProtoContract>]
    type OrderResult =
        { [<ProtoMember(1)>] Confirmation: OrderConfirmation
          [<ProtoMember(2)>] Error: string
          [<ProtoMember(3)>] ErrorCode: int }

    module OrderResult =
        let ok conf = { Confirmation = conf; Error = null; ErrorCode = 0 }
        let error code msg = { Confirmation = Unchecked.defaultof<_>; Error = msg; ErrorCode = code }
        let isOk r = r.ErrorCode = 0

    // ── Service Contracts ──

    [<ServiceContract>]
    type IGreeterService =
        [<OperationContract>]
        abstract SayHello: request: GreetRequest * context: CallContext -> ValueTask<GreetReply>

    [<ServiceContract>]
    type ISearchService =
        [<OperationContract>]
        abstract Search: request: SearchRequest * context: CallContext -> ValueTask<SearchReply>

    [<ServiceContract>]
    type IOrderService =
        [<OperationContract>]
        abstract PlaceOrder: request: Order * context: CallContext -> ValueTask<OrderResult>

    [<ServiceContract>]
    type ITickerService =
        [<OperationContract>]
        abstract Subscribe: request: SubscribeRequest * context: CallContext -> IAsyncEnumerable<TickData>

    [<ServiceContract>]
    type IChatService =
        [<OperationContract>]
        abstract Chat: messages: IAsyncEnumerable<ChatMessage> * context: CallContext -> IAsyncEnumerable<ChatMessage>
