namespace GrpcSkills.Tests

open ProtoBuf.FSharp
open GrpcSkills.Tests.Contracts

/// Register F# types with protobuf-net.
/// Call `Setup.ensureRegistered()` in any test that needs serialization.
module Setup =
    let private registered =
        lazy
            let model = Serialiser.defaultModel
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetRequest> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetReply> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<SearchRequest> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<SearchResult> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<SearchReply> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<OrderLine> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<Order> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<OrderConfirmation> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<OrderResult> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<Address> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<Person> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<TickData> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<SubscribeRequest> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<ChatMessage> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<DataChunk> model |> ignore
            Serialiser.registerRecordRuntimeTypeIntoModel typeof<UploadResult> model |> ignore

    let ensureRegistered () = registered.Force()
