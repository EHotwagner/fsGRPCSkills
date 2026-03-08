# gRPC Client Patterns for F#

## Channel Creation

### Basic channel

```fsharp
let channel = GrpcChannel.ForAddress("http://localhost:5000")
```

### With options

```fsharp
let channel =
    GrpcChannel.ForAddress("https://api.example.com",
        GrpcChannelOptions(
            MaxReceiveMessageSize = 16 * 1024 * 1024,
            MaxSendMessageSize = 16 * 1024 * 1024
        ))
```

### Insecure (development only)

```fsharp
// For HTTP (no TLS) in development
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true)
let channel = GrpcChannel.ForAddress("http://localhost:5000")
```

### With custom HttpHandler

```fsharp
let handler = new System.Net.Http.SocketsHttpHandler(
    PooledConnectionIdleTimeout = System.TimeSpan.FromMinutes(5.0),
    KeepAlivePingDelay = System.TimeSpan.FromSeconds(60.0),
    KeepAlivePingTimeout = System.TimeSpan.FromSeconds(30.0),
    EnableMultipleHttp2Connections = true
)

let channel =
    GrpcChannel.ForAddress("https://api.example.com",
        GrpcChannelOptions(HttpHandler = handler))
```

## Client Creation

### Code-first

```fsharp
open ProtoBuf.Grpc.Client

let service = channel.CreateGrpcService<IMyService>()
```

### Standard (contract-first)

```fsharp
let client = MyService.MyServiceClient(channel)
```

## Calling Patterns

### Unary call

```fsharp
let callUnary () = task {
    let! reply = service.DoSomething(request, CallContext.Default)
    return reply
}
```

### With timeout

```fsharp
let callWithTimeout () = task {
    use cts = new System.Threading.CancellationTokenSource(
        System.TimeSpan.FromSeconds(5.0))
    let ctx = CallContext(cancellationToken = cts.Token)
    let! reply = service.DoSomething(request, ctx)
    return reply
}
```

### With metadata

```fsharp
let callWithAuth token = task {
    let headers = Grpc.Core.Metadata()
    headers.Add("authorization", $"Bearer {token}")
    let ctx = CallContext(requestHeaders = headers)
    let! reply = service.DoSomething(request, ctx)
    return reply
}
```

### Server streaming consumption

```fsharp
let consumeStream () = task {
    let stream = service.StreamData(request, CallContext.Default)

    // Using IAsyncEnumerable
    let enumerator = stream.GetAsyncEnumerator()
    try
        while! enumerator.MoveNextAsync() do
            let item = enumerator.Current
            printfn $"Got: {item}"
    finally
        enumerator.DisposeAsync().AsTask().Wait()
}
```

### Client streaming

```fsharp
let sendStream () = task {
    let data = taskSeq {
        for i in 1..10 do
            yield { Value = i; Timestamp = System.DateTimeOffset.UtcNow }
    }

    let! result = service.UploadData(data, CallContext.Default)
    printfn $"Uploaded: {result.Count} items"
}
```

## Error Handling

### Comprehensive handler

```fsharp
open Grpc.Core

let handleGrpcCall (call: unit -> Task<'T>) = task {
    try
        let! result = call ()
        return Ok result
    with
    | :? RpcException as ex ->
        match ex.StatusCode with
        | StatusCode.NotFound ->
            return Error $"Not found: {ex.Status.Detail}"
        | StatusCode.InvalidArgument ->
            return Error $"Bad request: {ex.Status.Detail}"
        | StatusCode.Unauthenticated ->
            return Error "Authentication required"
        | StatusCode.PermissionDenied ->
            return Error "Permission denied"
        | StatusCode.Unavailable ->
            return Error "Service unavailable"
        | StatusCode.DeadlineExceeded ->
            return Error "Request timed out"
        | code ->
            return Error $"gRPC error ({code}): {ex.Status.Detail}"
}

// Usage:
let! result = handleGrpcCall (fun () ->
    service.DoSomething(request, CallContext.Default).AsTask())
```

## Dependency Injection

### Register in ASP.NET Core

```fsharp
// Register code-first client
builder.Services
    .AddCodeFirstGrpcClient<IMyService>(fun o ->
        o.Address <- System.Uri("http://localhost:5000"))
|> ignore

// Register standard client
builder.Services
    .AddGrpcClient<MyService.MyServiceClient>(fun o ->
        o.Address <- System.Uri("http://localhost:5000"))
|> ignore
```

### Inject and use

```fsharp
type MyHandler(greeter: IGreeterService) =
    member _.Handle() = task {
        let! reply = greeter.SayHello({ Name = "DI" }, CallContext.Default)
        return reply.Message
    }
```

## Resilience

### Retry with Polly

```fsharp
open Microsoft.Extensions.DependencyInjection

builder.Services
    .AddCodeFirstGrpcClient<IMyService>(fun o ->
        o.Address <- System.Uri("http://localhost:5000"))
    .AddPolicyHandler(
        Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, fun i -> System.TimeSpan.FromSeconds(float i)))
|> ignore
```

### Manual retry

```fsharp
let retryCall maxAttempts (call: unit -> Task<'T>) = task {
    let mutable attempt = 0
    let mutable result = Unchecked.defaultof<'T>
    let mutable success = false

    while not success && attempt < maxAttempts do
        attempt <- attempt + 1
        try
            let! r = call ()
            result <- r
            success <- true
        with
        | :? RpcException as ex when
            ex.StatusCode = StatusCode.Unavailable && attempt < maxAttempts ->
                do! Task.Delay(1000 * attempt)

    if not success then
        raise (System.Exception("All retry attempts failed"))

    return result
}
```
