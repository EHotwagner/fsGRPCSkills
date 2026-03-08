open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open ProtoBuf.Grpc.Server
open ProtoBuf.FSharp
open MyApp.Shared.Contracts
open MyApp.Server.Services

// Register F# types before building the host (each record type individually)
let model = Serialiser.defaultModel
Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetRequest> model |> ignore
Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetReply> model |> ignore

let builder = WebApplication.CreateBuilder()

// Add code-first gRPC services
builder.Services.AddCodeFirstGrpc() |> ignore

// Configure Kestrel for HTTP/2
builder.WebHost.ConfigureKestrel(fun options ->
    options.ListenLocalhost(5000, fun listenOptions ->
        listenOptions.Protocols <-
            Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2
    )
) |> ignore

let app = builder.Build()

// Map gRPC service endpoints
app.MapGrpcService<GreeterService>() |> ignore

app.Run()
