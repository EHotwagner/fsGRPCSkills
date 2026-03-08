namespace MyApp.Shared

open ProtoBuf
open System.ServiceModel
open System.Threading.Tasks
open ProtoBuf.Grpc

/// Data contracts — F# records with protobuf attributes.
module Contracts =

    // ── Request / Response Types ──

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

    // ── Service Contracts ──

    [<ServiceContract>]
    type IGreeterService =
        [<OperationContract>]
        abstract SayHello: request: GreetRequest * context: CallContext -> ValueTask<GreetReply>

    [<ServiceContract>]
    type ISearchService =
        [<OperationContract>]
        abstract Search: request: SearchRequest * context: CallContext -> ValueTask<SearchReply>
