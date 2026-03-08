---
name: fsgrpc-server
description: >-
  Use when the user asks to "implement a gRPC server", "create gRPC service
  implementation", "host a gRPC service", "write gRPC server in F#",
  "add gRPC endpoint", "configure gRPC server", or needs to implement and
  host gRPC services in an F# ASP.NET Core application.
version: 0.1.0
---

# gRPC Server Implementation in F#

Implement and host gRPC services using ASP.NET Core in F#.

## Overview

This skill covers implementing gRPC server services in F#, for both code-first
(protobuf-net.Grpc) and contract-first (FsGrpc / standard) approaches. The
server runs on ASP.NET Core with Kestrel, supporting HTTP/2 for native gRPC
and gRPC-Web for browser clients.

## Workflow

### 1. Create the server project

```xml
<!-- Server.fsproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <!-- Code-first -->
    <PackageReference Include="protobuf-net.Grpc.AspNetCore" Version="1.1.1" />
    <PackageReference Include="protobuf-net-fsharp" Version="0.1.0" />
    <!-- OR Contract-first (standard) -->
    <PackageReference Include="Grpc.AspNetCore" Version="2.67.0" />
    <!-- gRPC-Web support (works with either approach) -->
    <PackageReference Include="Grpc.AspNetCore.Web" Version="2.67.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Shared\Shared.fsproj" />
  </ItemGroup>
</Project>
```

### 2. Implement a service (code-first)

Implement the service interface defined in your shared contracts:

```fsharp
module MyApp.Server.Services

open System.Threading.Tasks
open ProtoBuf.Grpc
open MyApp.Shared.Contracts

type GreeterService() =
    interface IGreeterService with
        member _.SayHello(request, _context) =
            let reply =
                { Message = $"Hello, {request.Name}!" }
            ValueTask<GreetReply>(reply)
```

#### Server streaming implementation

```fsharp
open System.Collections.Generic
open System.Runtime.CompilerServices

type TickerService() =
    interface ITickerService with
        member _.Subscribe(request, context) =
            let produce (writer: ChannelWriter<TickData>) = task {
                for i in 1..100 do
                    if context.CancellationToken.IsCancellationRequested then
                        return ()
                    let tick = { Price = 100.0 + float i; Symbol = request.Symbol }
                    do! writer.WriteAsync(tick)
                    do! Task.Delay(1000)
            }
            // Return IAsyncEnumerable using a helper
            AsyncEnumerable.from produce
```

#### Using async computation expressions

```fsharp
open FSharp.Control

type OrderService() =
    interface IOrderService with
        member _.PlaceOrder(request, _context) =
            vtask {
                // Validate
                if Array.isEmpty request.Lines then
                    return ServiceResult.error 400 "Order must have at least one line"
                else
                    // Process
                    let orderId = System.Guid.NewGuid().ToString("N")
                    let confirmation =
                        { OrderId = orderId
                          Total = request.Lines |> Array.sumBy (fun l -> l.UnitPrice * decimal l.Quantity) }
                    return ServiceResult.ok confirmation
            }
```

### 3. Implement a service (contract-first / standard)

When using C#-generated types from Grpc.Tools:

```fsharp
module MyApp.Server.Services

open System.Threading.Tasks
open Grpc.Core
open MyApp.V1  // C# generated namespace

type GreeterServiceImpl() =
    inherit Greeter.GreeterBase()

    override _.SayHello(request, context) =
        let reply = GreetReply(Message = $"Hello, {request.Name}!")
        Task.FromResult(reply)

    override _.SayHelloStream(request, responseStream, context) = task {
        for i in 1..5 do
            let reply = GreetReply(Message = $"Hello #{i}, {request.Name}!")
            do! responseStream.WriteAsync(reply)
            do! Task.Delay(1000)
    }
```

### 4. Configure the host (code-first)

```fsharp
// Program.fs
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open ProtoBuf.Grpc.Server
open ProtoBuf.FSharp
open MyApp.Shared.Contracts
open MyApp.Server.Services

// Register F# types FIRST (each record type must be registered individually)
let model = Serialiser.defaultModel
Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetRequest> model |> ignore
Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetReply> model |> ignore

let builder = WebApplication.CreateBuilder()

// Add code-first gRPC
builder.Services.AddCodeFirstGrpc(fun options ->
    options.ResponseCompressionLevel <- System.IO.Compression.CompressionLevel.Optimal
)

// Configure Kestrel for HTTP/2
builder.WebHost.ConfigureKestrel(fun options ->
    options.ListenLocalhost(5000, fun listenOptions ->
        listenOptions.Protocols <- Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2
    )
) |> ignore

let app = builder.Build()

// Map services
app.MapGrpcService<GreeterService>() |> ignore

app.Run()
```

### 5. Configure the host (contract-first / standard)

```fsharp
// Program.fs
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open MyApp.Server.Services

let builder = WebApplication.CreateBuilder()
builder.Services.AddGrpc() |> ignore

let app = builder.Build()
app.MapGrpcService<GreeterServiceImpl>() |> ignore
app.Run()
```

### 6. Enable gRPC-Web

gRPC-Web lets browser clients (JavaScript/TypeScript, Blazor WASM) call gRPC
services over HTTP/1.1. Native gRPC requires HTTP/2 with trailers, which
browsers don't expose — gRPC-Web bridges that gap.

#### Add the middleware

```fsharp
open Grpc.AspNetCore.Web

let app = builder.Build()

// Enable gRPC-Web for all services (before MapGrpcService calls)
app.UseGrpcWeb(GrpcWebOptions(DefaultEnabled = true)) |> ignore

app.MapGrpcService<GreeterService>() |> ignore
```

Or enable per-service instead of globally:

```fsharp
app.UseGrpcWeb() |> ignore

app.MapGrpcService<GreeterService>().EnableGrpcWeb() |> ignore
app.MapGrpcService<InternalService>() |> ignore  // gRPC-Web NOT enabled
```

#### Configure Kestrel for HTTP/1.1 + HTTP/2

gRPC-Web traffic arrives over HTTP/1.1, so Kestrel must accept both protocols:

```fsharp
builder.WebHost.ConfigureKestrel(fun options ->
    options.ListenLocalhost(5000, fun listenOptions ->
        listenOptions.Protocols <-
            Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2
    )
) |> ignore
```

> **Note:** With `Http1AndHttp2`, native gRPC clients still negotiate HTTP/2
> automatically. Only use `Http2` alone when you are certain no browser clients
> will connect.

#### Add CORS for browser clients

Browser gRPC-Web requests are cross-origin and require CORS:

```fsharp
open Microsoft.Extensions.DependencyInjection

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

let app = builder.Build()

app.UseCors("GrpcWeb") |> ignore
app.UseGrpcWeb(GrpcWebOptions(DefaultEnabled = true)) |> ignore

app.MapGrpcService<GreeterService>().RequireCors("GrpcWeb") |> ignore
```

For production, replace `AllowAnyOrigin()` with `WithOrigins("https://myapp.com")`.

#### Contract-first gRPC-Web

The middleware works identically with contract-first services:

```fsharp
builder.Services.AddGrpc() |> ignore

let app = builder.Build()
app.UseGrpcWeb(GrpcWebOptions(DefaultEnabled = true)) |> ignore
app.MapGrpcService<GreeterServiceImpl>() |> ignore
```

### 7. Add server reflection (development)

Server reflection lets tools like `grpcurl` discover services:

```fsharp
// Add to services
builder.Services.AddGrpcReflection() |> ignore

// Add to pipeline (development only)
#if DEBUG
app.MapGrpcReflectionService() |> ignore
#endif
```

### 8. Interceptors

```fsharp
open Grpc.Core
open Grpc.Core.Interceptors

type LoggingInterceptor(logger: ILogger<LoggingInterceptor>) =
    inherit Interceptor()

    override _.UnaryServerHandler(request, context, continuation) = task {
        let sw = System.Diagnostics.Stopwatch.StartNew()
        try
            let! response = continuation.Invoke(request, context)
            sw.Stop()
            logger.LogInformation("gRPC {Method} completed in {Elapsed}ms",
                context.Method, sw.ElapsedMilliseconds)
            return response
        with ex ->
            sw.Stop()
            logger.LogError(ex, "gRPC {Method} failed after {Elapsed}ms",
                context.Method, sw.ElapsedMilliseconds)
            raise ex
    }
```

Register it:

```fsharp
builder.Services.AddCodeFirstGrpc(fun options ->
    options.Interceptors.Add<LoggingInterceptor>()
)
```

### 9. Health checks

```fsharp
builder.Services.AddGrpcHealthChecks()
                .AddCheck("self", fun () ->
                    Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
|> ignore
```

### 10. Error handling

Use `RpcException` with standard gRPC status codes:

```fsharp
open Grpc.Core

let validateRequest (request: CreateUserRequest) =
    if System.String.IsNullOrWhiteSpace(request.Email) then
        raise (RpcException(Status(StatusCode.InvalidArgument, "Email is required")))
    if request.Age < 0 then
        raise (RpcException(Status(StatusCode.InvalidArgument, "Age must be non-negative")))
```

Standard gRPC status codes for F#:

| Code | When to use |
|------|-------------|
| `OK` | Success (default) |
| `InvalidArgument` | Bad input from client |
| `NotFound` | Resource doesn't exist |
| `AlreadyExists` | Duplicate creation |
| `PermissionDenied` | Auth failure |
| `Unauthenticated` | Missing credentials |
| `ResourceExhausted` | Rate limiting |
| `Internal` | Unexpected server error |
| `Unavailable` | Transient failure, client should retry |
| `DeadlineExceeded` | Operation timed out |

## Best Practices

- **DO** register F# types before building the web host
- **DO** use `ValueTask<T>` for synchronous-path returns (avoids allocation)
- **DO** check `context.CancellationToken` in long-running operations
- **DO** use gRPC status codes, not HTTP status codes
- **DO** add server reflection in development for debugging with grpcurl
- **DO** configure HTTP/2 explicitly on Kestrel (use `Http1AndHttp2` when serving gRPC-Web)
- **DO** add CORS when exposing gRPC-Web to browser clients
- **DO** place `UseGrpcWeb()` before `MapGrpcService` calls in the pipeline
- **DON'T** throw generic exceptions; use `RpcException` with appropriate status codes
- **DON'T** return `null` from service methods; always return a valid response
- **DON'T** block the thread with `.Result` or `.Wait()`; use `task {}` or `vtask {}`
- **DON'T** forget to map services with `app.MapGrpcService<T>()`

## Additional Resources

- Templates: `templates/Program.fs`, `templates/Service.fs`, `templates/Server.fsproj`
- Reference: `references/server-patterns.md`

## Implementation Workflow

1. Create server project with `Microsoft.NET.Sdk.Web`
2. Add gRPC NuGet packages (including `Grpc.AspNetCore.Web`) and project reference to shared contracts
3. Register F# types (code-first only)
4. Implement service class(es)
5. Configure host with gRPC services and gRPC-Web middleware
6. Add CORS policy if serving browser clients
7. Add interceptors, health checks, reflection as needed
8. Run and verify with `grpcurl`, a test client, or browser gRPC-Web client
