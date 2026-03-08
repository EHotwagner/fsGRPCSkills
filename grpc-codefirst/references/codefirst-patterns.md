# Code-First gRPC Patterns for F#

## Registration Patterns

### Basic registration

```fsharp
open ProtoBuf.FSharp

// Call once at startup, before any serialization.
// Register each F# record type individually.
let model = Serialiser.defaultModel

Serialiser.registerRecordRuntimeTypeIntoModel typeof<MyRequest> model |> ignore
Serialiser.registerRecordRuntimeTypeIntoModel typeof<MyReply> model |> ignore
```

> The module is `ProtoBuf.FSharp.Serialiser` (British spelling).

### Registration helper

```fsharp
/// Register multiple F# record types at once.
let registerAll (types: System.Type list) =
    let model = Serialiser.defaultModel
    for t in types do
        Serialiser.registerRecordRuntimeTypeIntoModel t model |> ignore
```

## Contract Patterns

### Unary RPC

```fsharp
[<ServiceContract>]
type IMyService =
    [<OperationContract>]
    abstract DoSomething: request: MyRequest * context: CallContext -> ValueTask<MyReply>
```

### Server Streaming

```fsharp
[<ServiceContract>]
type IMyService =
    [<OperationContract>]
    abstract StreamData: request: MyRequest * context: CallContext
        -> IAsyncEnumerable<MyItem>
```

### Client Streaming

```fsharp
[<ServiceContract>]
type IMyService =
    [<OperationContract>]
    abstract UploadData: data: IAsyncEnumerable<MyChunk> * context: CallContext
        -> ValueTask<UploadResult>
```

### Bidirectional Streaming

```fsharp
[<ServiceContract>]
type IMyService =
    [<OperationContract>]
    abstract Chat: messages: IAsyncEnumerable<ChatMsg> * context: CallContext
        -> IAsyncEnumerable<ChatMsg>
```

## Data Contract Patterns

### Simple record

```fsharp
[<ProtoContract>]
type Point =
    { [<ProtoMember(1)>] X: float
      [<ProtoMember(2)>] Y: float }
```

### Record with optional fields

```fsharp
[<ProtoContract>]
type UserProfile =
    { [<ProtoMember(1)>] Id: string
      [<ProtoMember(2)>] Name: string
      [<ProtoMember(3)>] Email: string option
      [<ProtoMember(4)>] Age: int option }
```

### Record with collections

> F# `list<T>` does NOT work with protobuf-net. Use `array` (`T[]`).
> F# `Map<K,V>` does not work. Use `System.Collections.Generic.Dictionary<K,V>`.

```fsharp
open System.Collections.Generic

[<ProtoContract>]
type Inventory =
    { [<ProtoMember(1)>] Items: Item array
      [<ProtoMember(2)>] Metadata: Dictionary<string, string> }
```

### Discriminated union (simple)

```fsharp
[<ProtoContract>]
[<ProtoInclude(10, "Circle")>]
[<ProtoInclude(11, "Rectangle")>]
type Shape =
    | Circle of radius: float
    | Rectangle of width: float * height: float
```

### Nested records

```fsharp
[<ProtoContract>]
type Address =
    { [<ProtoMember(1)>] Street: string
      [<ProtoMember(2)>] City: string
      [<ProtoMember(3)>] PostCode: string }

[<ProtoContract>]
type Person =
    { [<ProtoMember(1)>] Name: string
      [<ProtoMember(2)>] Address: Address }
```

### Enum-like DU (no data)

For DUs with no data fields, use F# enums instead:

```fsharp
[<ProtoContract>]
type Priority =
    | [<ProtoEnum>] Low = 0
    | [<ProtoEnum>] Medium = 1
    | [<ProtoEnum>] High = 2
```

## Common Pitfalls

| Issue | Cause | Fix |
|-------|-------|-----|
| `InvalidOperationException` on F# record | Missing F# registration | Call `Serialiser.registerRecordRuntimeTypeIntoModel` for each type |
| F# `list` or `Map` fails | Unsupported collection type | Use `array` (`T[]`) and `Dictionary<K,V>` instead |
| `None` fails to serialize | Missing `protobuf-net-fsharp` | Add the NuGet package |
| `Async<T>` not supported | Wrong async type | Use `ValueTask<T>` or `Task<T>` |
| Anonymous record fails | Not supported by protobuf-net | Use named records |
| Field always default | Wrong or missing `ProtoMember` number | Check attribute numbers |
| DU fails at runtime | Missing `ProtoInclude` | Add `ProtoInclude` for each case |
