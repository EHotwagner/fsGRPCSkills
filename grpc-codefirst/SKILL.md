---
name: grpc-codefirst
description: >-
  Use when the user asks to "create gRPC without proto files", "code-first gRPC",
  "define gRPC service in F#", "use protobuf-net with F#", "gRPC with F# records",
  "gRPC without .proto", or wants to define gRPC contracts using native F# types
  instead of .proto files.
version: 0.1.0
---

# Code-First gRPC with F#

Define gRPC services using native F# types and interfaces, without .proto files.

## Overview

Code-first gRPC uses `protobuf-net.Grpc` to derive service contracts from .NET
interfaces and data types. Combined with `protobuf-net-fsharp`, this approach
lets you write fully idiomatic F# with records, options, and discriminated unions
as your wire types.

**Key packages**:

| Package | Purpose |
|---------|---------|
| `protobuf-net` | Protobuf serialization |
| `protobuf-net-fsharp` | F# type support (records, options, DUs) |
| `protobuf-net.Grpc` | Code-first gRPC core |
| `protobuf-net.Grpc.AspNetCore` | Server hosting on ASP.NET Core |
| `protobuf-net.Grpc.Native` | Client using native gRPC channel |
| `System.ServiceModel.Primitives` | `[ServiceContract]` / `[OperationContract]` attributes |

## Workflow

### 1. Define data contracts

Use F# records with `[<ProtoContract>]` and `[<ProtoMember>]` attributes:

```fsharp
open ProtoBuf

[<ProtoContract>]
type GreetRequest =
    { [<ProtoMember(1)>] Name: string }

[<ProtoContract>]
type GreetReply =
    { [<ProtoMember(1)>] Message: string }
```

#### Handling F# option types

```fsharp
[<ProtoContract>]
type SearchRequest =
    { [<ProtoMember(1)>] Query: string
      [<ProtoMember(2)>] MaxResults: int option
      [<ProtoMember(3)>] Category: string option }
```

> `protobuf-net-fsharp` serializes `option` correctly. Without it,
> `None` values cause runtime errors.

#### Handling discriminated unions

```fsharp
[<ProtoContract>]
[<ProtoInclude(10, "CreditCard")>]
[<ProtoInclude(11, "BankTransfer")>]
type PaymentMethod =
    | [<ProtoMember(1)>] CreditCard of cardNumber: string * expiry: string
    | [<ProtoMember(2)>] BankTransfer of iban: string
```

> DU support requires `protobuf-net-fsharp` registration. Simple DUs work
> well; deeply nested DUs may require manual handling.

#### Records with collections

> **Important**: F# `list<T>` does NOT work with protobuf-net for repeated fields.
> Use `array` (`T[]`) instead. Similarly, F# `Map<K,V>` does not work; use
> `System.Collections.Generic.Dictionary<K,V>`.

```fsharp
open System.Collections.Generic

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
```

### 2. Define service contracts

Use `[<ServiceContract>]` interfaces with `[<OperationContract>]` methods.
Return types must be `ValueTask<T>` or `Task<T>`:

```fsharp
open System.ServiceModel
open System.Threading.Tasks
open ProtoBuf.Grpc

[<ServiceContract>]
type IGreeterService =
    [<OperationContract>]
    abstract SayHello: request: GreetRequest * context: CallContext -> ValueTask<GreetReply>
```

#### Streaming contracts

```fsharp
open System.Collections.Generic
open System.Runtime.CompilerServices

[<ServiceContract>]
type IChatService =
    // Server streaming: returns IAsyncEnumerable
    [<OperationContract>]
    abstract Subscribe: request: SubscribeRequest * context: CallContext
        -> IAsyncEnumerable<ChatMessage>

    // Client streaming: accepts IAsyncEnumerable
    [<OperationContract>]
    abstract Upload: data: IAsyncEnumerable<DataChunk> * context: CallContext
        -> ValueTask<UploadResult>

    // Bidirectional streaming
    [<OperationContract>]
    abstract Chat: messages: IAsyncEnumerable<ChatMessage> * context: CallContext
        -> IAsyncEnumerable<ChatMessage>
```

### 3. Register F# types

**Critical step** - register each F# record type at application startup,
before any serialization occurs:

```fsharp
open ProtoBuf.FSharp

let registerTypes () =
    let model = Serialiser.defaultModel
    Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetRequest> model |> ignore
    Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetReply> model |> ignore
    // ... register all F# record types used in contracts
```

> The module is `ProtoBuf.FSharp.Serialiser` (British spelling).

Call `registerTypes()` in your `Program.fs` before building the host or
creating any gRPC channels.

### 4. Shared contracts project

Keep contracts in a standalone project with no server/client dependencies:

```xml
<!-- Shared.fsproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="protobuf-net" Version="3.2.45" />
    <PackageReference Include="protobuf-net-fsharp" Version="0.1.0" />
    <PackageReference Include="protobuf-net.Grpc" Version="1.1.1" />
    <PackageReference Include="System.ServiceModel.Primitives" Version="8.1.0" />
  </ItemGroup>
</Project>
```

### 5. Common patterns

#### Empty request/response

```fsharp
[<ProtoContract>]
type Empty = { __dummy: unit }
// Or use a marker:
[<ProtoContract>]
type Unit' = class end
```

#### Wrapper for single values

```fsharp
[<ProtoContract>]
type StringValue =
    { [<ProtoMember(1)>] Value: string }

[<ProtoContract>]
type Int32Value =
    { [<ProtoMember(1)>] Value: int }
```

#### Error handling pattern

> **Note**: Generic records with `option` (like `ServiceResult<'T>`) can be tricky
> with protobuf-net. Prefer non-generic concrete types for result patterns.

```fsharp
[<ProtoContract>]
type ServiceError =
    { [<ProtoMember(1)>] Message: string
      [<ProtoMember(2)>] ErrorCode: int }

[<ProtoContract>]
type OrderResult =
    { [<ProtoMember(1)>] OrderId: string option
      [<ProtoMember(2)>] Error: ServiceError option }

module ServiceResult =
    let ok orderId = { OrderId = Some orderId; Error = None }
    let error code msg = { OrderId = None; Error = Some { Message = msg; ErrorCode = code } }
```

## Best Practices

- **DO** register each F# record type with `Serialiser.registerRecordRuntimeTypeIntoModel` before any serialization
- **DO** use `array` (`T[]`) for repeated fields, NOT F# `list<T>`
- **DO** use `Dictionary<K,V>` for map fields, NOT F# `Map<K,V>`
- **DO** prefer non-generic concrete types for result/error patterns
- **DO** keep all contracts (records + interfaces) in a shared project
- **DO** use `ValueTask<T>` for return types (better performance than `Task<T>`)
- **DO** include `CallContext` as the last parameter in service methods
- **DO** use `[<ProtoMember(N)>]` with explicit, stable field numbers
- **DO** use F# `option` for optional fields with `protobuf-net-fsharp`
- **DON'T** reuse field numbers across versions
- **DON'T** forget to install `protobuf-net-fsharp`; without it, F# records fail at runtime
- **DON'T** use anonymous records as proto contracts
- **DON'T** nest DUs more than one level deep in proto contracts
- **DON'T** use `Async<T>` in service interfaces; use `ValueTask<T>` or `Task<T>`

## Additional Resources

- [protobuf-net.Grpc Getting Started](https://protobuf-net.github.io/protobuf-net.Grpc/gettingstarted)
- [protobuf-net-fsharp](https://github.com/mlivernoche/protobuf-net-fsharp)
- Templates: `templates/Contracts.fs`, `templates/Shared.fsproj`
- Reference: `references/codefirst-patterns.md`

## Implementation Workflow

1. Create shared contracts project with NuGet references
2. Define request/response records with `[<ProtoContract>]`
3. Define service interfaces with `[<ServiceContract>]`
4. Register F# types in both server and client startup
5. Build and verify all contracts compile
6. Implement server (see grpc-server skill) and client (see grpc-client skill)
