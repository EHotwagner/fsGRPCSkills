open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open ProtoBuf.Grpc.Server
open ProtoBuf.FSharp
open Grpc.AspNetCore.Web
open MyApp.Shared.Contracts
open MyApp.Server.Services

// Register F# types before building the host (each record type individually)
let model = Serialiser.defaultModel
Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetRequest> model |> ignore
Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetReply> model |> ignore

let builder = WebApplication.CreateBuilder()

// Add code-first gRPC services
builder.Services.AddCodeFirstGrpc() |> ignore

// Add CORS for browser gRPC-Web clients
builder.Services.AddCors(fun options ->
    options.AddPolicy("GrpcWeb", fun policy ->
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding")
        |> ignore
    )
) |> ignore

// Configure Kestrel for HTTP/1.1 + HTTP/2 (required for gRPC-Web)
builder.WebHost.ConfigureKestrel(fun options ->
    options.ListenLocalhost(5000, fun listenOptions ->
        listenOptions.Protocols <-
            Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2
    )
) |> ignore

let app = builder.Build()

// Middleware order: CORS → gRPC-Web → routing
app.UseCors("GrpcWeb") |> ignore
app.UseGrpcWeb(GrpcWebOptions(DefaultEnabled = true)) |> ignore

// Map gRPC service endpoints
app.MapGrpcService<GreeterService>().RequireCors("GrpcWeb") |> ignore

app.Run()
