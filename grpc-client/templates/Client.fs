module MyApp.Client.Main

open System.Threading.Tasks
open Grpc.Net.Client
open ProtoBuf.Grpc.Client
open ProtoBuf.FSharp
open MyApp.Shared.Contracts

/// Register F# types for protobuf-net serialization.
let init () =
    let model = Serialiser.defaultModel
    Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetRequest> model |> ignore
    Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetReply> model |> ignore

/// Create a typed gRPC client from a channel.
let createGreeterClient (channel: GrpcChannel) =
    channel.CreateGrpcService<IGreeterService>()

/// Call the SayHello RPC.
let sayHello (client: IGreeterService) (name: string) = task {
    let request = { Name = name }
    let! reply = client.SayHello(request, ProtoBuf.Grpc.CallContext.Default)
    return reply.Message
}

[<EntryPoint>]
let main _argv =
    init ()

    use channel = GrpcChannel.ForAddress("http://localhost:5000")
    let client = createGreeterClient channel

    let message =
        sayHello client "World"
        |> Async.AwaitTask
        |> Async.RunSynchronously

    printfn $"%s{message}"
    0
