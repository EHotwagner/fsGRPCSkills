# gRPC Server Patterns for F#

## Host Configuration

### Minimal server (code-first)

```fsharp
open Microsoft.AspNetCore.Builder
open ProtoBuf.Grpc.Server
open ProtoBuf.FSharp

// Register each F# record type individually
let model = Serialiser.defaultModel
Serialiser.registerRecordRuntimeTypeIntoModel typeof<MyRequest> model |> ignore
Serialiser.registerRecordRuntimeTypeIntoModel typeof<MyReply> model |> ignore

let builder = WebApplication.CreateBuilder()
builder.Services.AddCodeFirstGrpc() |> ignore

let app = builder.Build()
app.MapGrpcService<MyService>() |> ignore
app.Run()
```

### With compression

```fsharp
builder.Services.AddCodeFirstGrpc(fun options ->
    options.ResponseCompressionLevel <-
        System.IO.Compression.CompressionLevel.Optimal
    options.ResponseCompressionAlgorithm <- "gzip"
)
```

### With message size limits

```fsharp
builder.Services.AddCodeFirstGrpc(fun options ->
    options.MaxReceiveMessageSize <- 16 * 1024 * 1024  // 16 MB
    options.MaxSendMessageSize <- 16 * 1024 * 1024
)
```

### With TLS (production)

```fsharp
builder.WebHost.ConfigureKestrel(fun options ->
    options.ListenAnyIP(5001, fun listenOptions ->
        listenOptions.Protocols <-
            Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2
        listenOptions.UseHttps("/path/to/cert.pfx", "password")
    )
)
```

### HTTP/2 + HTTP/1.1 (for gRPC-Web compatibility)

```fsharp
builder.WebHost.ConfigureKestrel(fun options ->
    options.ListenLocalhost(5000, fun o ->
        o.Protocols <-
            Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2
    )
)
// Also add gRPC-Web middleware
app.UseGrpcWeb(GrpcWebOptions(DefaultEnabled = true)) |> ignore
```

## Service Implementation Patterns

### Dependency injection

```fsharp
type UserService(db: IDbContext, logger: ILogger<UserService>) =
    interface IUserService with
        member _.GetUser(request, _context) = vtask {
            logger.LogInformation("Getting user {Id}", request.Id)
            match! db.Users.FindAsync(request.Id) with
            | null ->
                raise (RpcException(Status(StatusCode.NotFound, "User not found")))
            | user ->
                return { Id = user.Id; Name = user.Name; Email = user.Email }
        }
```

Register the service with DI:

```fsharp
builder.Services.AddScoped<UserService>() |> ignore
```

### Server streaming with cancellation

```fsharp
member _.StreamUpdates(request, context) =
    let produce () = taskSeq {
        while not context.CancellationToken.IsCancellationRequested do
            let! update = getNextUpdate request.SubscriptionId
            yield update
            do! Task.Delay(100, context.CancellationToken)
    }
    produce ()
```

### Validation pattern

```fsharp
module Validation =
    let require field value =
        if System.String.IsNullOrWhiteSpace(value) then
            raise (RpcException(
                Status(StatusCode.InvalidArgument, $"{field} is required")))
        value

    let requirePositive field (value: int) =
        if value <= 0 then
            raise (RpcException(
                Status(StatusCode.InvalidArgument, $"{field} must be positive")))
        value

// Usage:
member _.CreateOrder(request, _context) = vtask {
    let name = Validation.require "Name" request.Name
    let qty = Validation.requirePositive "Quantity" request.Quantity
    // ...
}
```

### Error mapping

```fsharp
let mapException (ex: exn) =
    match ex with
    | :? System.ArgumentException as ae ->
        RpcException(Status(StatusCode.InvalidArgument, ae.Message))
    | :? System.UnauthorizedAccessException ->
        RpcException(Status(StatusCode.PermissionDenied, "Access denied"))
    | :? System.Collections.Generic.KeyNotFoundException ->
        RpcException(Status(StatusCode.NotFound, "Resource not found"))
    | _ ->
        RpcException(Status(StatusCode.Internal, "An internal error occurred"))
```

## Interceptor Patterns

### Logging interceptor

```fsharp
type LoggingInterceptor(logger: ILogger<LoggingInterceptor>) =
    inherit Grpc.Core.Interceptors.Interceptor()

    override _.UnaryServerHandler(request, context, continuation) = task {
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let method = context.Method
        try
            let! result = continuation.Invoke(request, context)
            logger.LogInformation("{Method} completed in {Ms}ms", method, sw.ElapsedMilliseconds)
            return result
        with ex ->
            logger.LogError(ex, "{Method} failed in {Ms}ms", method, sw.ElapsedMilliseconds)
            raise ex
    }
```

### Auth interceptor

```fsharp
type AuthInterceptor() =
    inherit Grpc.Core.Interceptors.Interceptor()

    override _.UnaryServerHandler(request, context, continuation) = task {
        let token =
            context.RequestHeaders
            |> Seq.tryFind (fun e -> e.Key = "authorization")
            |> Option.map (fun e -> e.Value)

        match token with
        | Some t when t.StartsWith("Bearer ") ->
            // Validate token...
            return! continuation.Invoke(request, context)
        | _ ->
            raise (RpcException(Status(StatusCode.Unauthenticated, "Missing token")))
            return Unchecked.defaultof<_>
    }
```

## Testing Patterns

### In-process test server

```fsharp
open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Builder

let createTestServer () =
    let builder = WebApplication.CreateBuilder()
    builder.Services.AddCodeFirstGrpc() |> ignore
    builder.WebHost.UseTestServer() |> ignore

    let app = builder.Build()
    app.MapGrpcService<GreeterService>() |> ignore
    app.Start()

    let client = app.GetTestClient()
    // Use client.BaseAddress to create a GrpcChannel
    GrpcChannel.ForAddress(client.BaseAddress, GrpcChannelOptions(
        HttpHandler = client.CreateHandler()
    ))
```
