---
name: grpc-client
description: >-
  Use when the user asks to "call a gRPC service", "create gRPC client",
  "consume gRPC service from F#", "connect to gRPC server", "write gRPC
  client code", "gRPC client streaming", or needs to consume gRPC services
  from F# client code.
version: 0.1.0
---

# gRPC Client in F#

Consume gRPC services from F# client code.

## Overview

This skill covers creating gRPC clients in F# for both code-first
(protobuf-net.Grpc) and contract-first (standard) approaches. Clients
can be used in console apps, web APIs, background services, or test projects.

## Workflow

### 1. Create the client project

```xml
<!-- Client.fsproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <!-- Code-first -->
    <PackageReference Include="protobuf-net.Grpc.Native" Version="1.1.1" />
    <PackageReference Include="protobuf-net-fsharp" Version="0.1.0" />
    <!-- OR standard contract-first -->
    <PackageReference Include="Grpc.Net.Client" Version="2.67.0" />
    <PackageReference Include="Google.Protobuf" Version="3.29.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Shared\Shared.fsproj" />
  </ItemGroup>
</Project>
```

### 2. Create a client (code-first)

```fsharp
open Grpc.Net.Client
open ProtoBuf.Grpc.Client
open ProtoBuf.FSharp
open MyApp.Shared.Contracts

// Register F# types (each record type individually)
let model = Serialiser.defaultModel
Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetRequest> model |> ignore
Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetReply> model |> ignore

// Create channel
let channel = GrpcChannel.ForAddress("http://localhost:5000")

// Create typed client from interface
let greeter = channel.CreateGrpcService<IGreeterService>()

// Call the service
let callGreeter () = task {
    let request = { Name = "World" }
    let! reply = greeter.SayHello(request, ProtoBuf.Grpc.CallContext.Default)
    printfn $"Response: {reply.Message}"
}
```

### 3. Create a client (contract-first / standard)

```fsharp
open Grpc.Net.Client
open MyApp.V1  // C# generated namespace

let channel = GrpcChannel.ForAddress("http://localhost:5000")
let client = Greeter.GreeterClient(channel)

let callGreeter () = task {
    let request = GreetRequest(Name = "World")
    let! reply = client.SayHelloAsync(request)
    printfn $"Response: {reply.Message}"
}
```

### 4. Streaming patterns

#### Server streaming (receive stream of responses)

```fsharp
let subscribeToTicker () = task {
    let request = { Symbol = "MSFT" }

    // Code-first: returns IAsyncEnumerable
    let stream = ticker.Subscribe(request, ProtoBuf.Grpc.CallContext.Default)
    do! stream |> AsyncSeq.iterAsync (fun tick ->
        printfn $"{tick.Symbol}: {tick.Price}"
        Task.CompletedTask
    )
}

// Standard approach:
let subscribeStandard () = task {
    use call = client.SayHelloStream(GreetRequest(Name = "World"))
    let stream = call.ResponseStream

    while! stream.MoveNext(System.Threading.CancellationToken.None) do
        printfn $"Got: {stream.Current.Message}"
}
```

#### Client streaming (send stream of requests)

```fsharp
let uploadData () = task {
    let chunks = asyncSeq {
        for i in 1..10 do
            yield { Data = $"chunk-{i}"; Index = i }
    }

    let! result = uploader.Upload(chunks, ProtoBuf.Grpc.CallContext.Default)
    printfn $"Upload result: {result.BytesReceived}"
}
```

#### Bidirectional streaming

```fsharp
let chat () = task {
    let outgoing = asyncSeq {
        yield { Text = "Hello"; User = "Alice" }
        do! Task.Delay(1000)
        yield { Text = "How are you?"; User = "Alice" }
    }

    let incoming = chatService.Chat(outgoing, ProtoBuf.Grpc.CallContext.Default)
    do! incoming |> AsyncSeq.iterAsync (fun msg ->
        printfn $"[{msg.User}]: {msg.Text}"
        Task.CompletedTask
    )
}
```

### 5. Channel management

#### Reuse channels

```fsharp
// Create once, reuse across the application lifetime
let channel = GrpcChannel.ForAddress("http://localhost:5000")

// Channels are thread-safe and multiplexed
let service1 = channel.CreateGrpcService<IGreeterService>()
let service2 = channel.CreateGrpcService<IOrderService>()
```

#### Channel with options

```fsharp
let channel =
    GrpcChannel.ForAddress(
        "https://api.example.com",
        GrpcChannelOptions(
            MaxReceiveMessageSize = 16 * 1024 * 1024,  // 16 MB
            MaxSendMessageSize = 16 * 1024 * 1024,
            Credentials = ChannelCredentials.Insecure   // Dev only
        )
    )
```

### 6. Dependency injection (ASP.NET Core client)

Register gRPC clients in an ASP.NET Core application:

```fsharp
// In ConfigureServices / builder.Services
builder.Services
    .AddCodeFirstGrpcClient<IGreeterService>(fun options ->
        options.Address <- System.Uri("http://localhost:5000")
    )
    .ConfigureChannel(fun options ->
        options.MaxReceiveMessageSize <- 16 * 1024 * 1024
    )
|> ignore
```

Then inject the service interface:

```fsharp
type MyController(greeter: IGreeterService) =
    member _.Get() = task {
        let! reply = greeter.SayHello({ Name = "Web" }, CallContext.Default)
        return reply.Message
    }
```

### 7. Error handling

```fsharp
open Grpc.Core

let callWithErrorHandling () = task {
    try
        let! reply = greeter.SayHello({ Name = "" }, CallContext.Default)
        printfn $"Success: {reply.Message}"
    with
    | :? RpcException as ex when ex.StatusCode = StatusCode.InvalidArgument ->
        printfn $"Validation error: {ex.Status.Detail}"
    | :? RpcException as ex when ex.StatusCode = StatusCode.Unavailable ->
        printfn "Server is unavailable, retrying..."
    | :? RpcException as ex ->
        printfn $"gRPC error {ex.StatusCode}: {ex.Status.Detail}"
}
```

### 8. Deadlines and cancellation

```fsharp
open System.Threading

let callWithDeadline () = task {
    use cts = new CancellationTokenSource(TimeSpan.FromSeconds(5.0))

    // Code-first: use CallContext with headers and cancellation
    let context = CallContext(cancellationToken = cts.Token)
    let! reply = greeter.SayHello({ Name = "World" }, context)
    printfn $"{reply.Message}"
}

// Standard approach:
let callWithDeadlineStandard () = task {
    let deadline = System.DateTime.UtcNow.AddSeconds(5.0)
    let! reply = client.SayHelloAsync(
        GreetRequest(Name = "World"),
        deadline = deadline
    )
    printfn $"{reply.Message}"
}
```

### 9. Metadata / headers

```fsharp
let callWithMetadata () = task {
    let headers = Metadata()
    headers.Add("authorization", "Bearer my-token")
    headers.Add("x-request-id", System.Guid.NewGuid().ToString())

    // Code-first
    let context = CallContext(requestHeaders = headers)
    let! reply = greeter.SayHello({ Name = "World" }, context)
    return reply
}
```

### 10. Retry policies

```fsharp
let retryChannel =
    GrpcChannel.ForAddress(
        "http://localhost:5000",
        GrpcChannelOptions(
            ServiceConfig = ServiceConfig(
                MethodConfigs = {|
                    Names = [| MethodName.Default |]
                    RetryPolicy = RetryPolicy(
                        MaxAttempts = 5,
                        InitialBackoff = TimeSpan.FromSeconds(1.0),
                        MaxBackoff = TimeSpan.FromSeconds(5.0),
                        BackoffMultiplier = 1.5,
                        RetryableStatusCodes = {| StatusCode.Unavailable |}
                    )
                |}
            )
        )
    )
```

## Best Practices

- **DO** register F# types before creating any channels (code-first)
- **DO** reuse `GrpcChannel` instances; they are thread-safe and handle connection pooling
- **DO** set deadlines on all RPCs to prevent hanging calls
- **DO** handle `RpcException` with pattern matching on `StatusCode`
- **DO** use `CancellationToken` for long-running streaming calls
- **DO** dispose channels when the application shuts down
- **DON'T** create a new channel per request
- **DON'T** ignore cancellation tokens in streaming consumers
- **DON'T** use `.Result` or `.Wait()`; use `task {}` computation expression
- **DON'T** catch and swallow `RpcException` without logging

## Additional Resources

- Templates: `templates/Client.fs`, `templates/Client.fsproj`
- Reference: `references/client-patterns.md`

## Implementation Workflow

1. Create client project with appropriate NuGet packages
2. Register F# types (code-first only)
3. Create `GrpcChannel` with server address
4. Create typed service client
5. Call RPCs with proper error handling and deadlines
6. Handle streaming responses with async iteration
7. Dispose channel on shutdown
