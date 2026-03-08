# fsGRPCSkills

Agent skills for using and creating gRPC from F#. Covers both **code-first** (protobuf-net) and **contract-first** (.proto) approaches with templates, reference docs, and an extensive test suite.

## Skills

| Skill | Description |
|-------|-------------|
| [grpc-setup](grpc-setup/SKILL.md) | Bootstrap gRPC tooling — choose an approach, add packages, scaffold project structure |
| [grpc-proto](grpc-proto/SKILL.md) | Write `.proto` files and generate idiomatic F# code (FsGrpc / Grpc.Tools) |
| [grpc-codefirst](grpc-codefirst/SKILL.md) | Define gRPC contracts with native F# records and interfaces (protobuf-net.Grpc) |
| [grpc-server](grpc-server/SKILL.md) | Implement and host gRPC services on ASP.NET Core |
| [grpc-client](grpc-client/SKILL.md) | Consume gRPC services from F# client code |

## Approaches

### Code-First (protobuf-net)

No `.proto` files needed. Define contracts in F# using records, interfaces, and attributes:

```fsharp
[<ProtoContract>]
type GreetRequest =
    { [<ProtoMember(1)>] Name: string }

[<ServiceContract>]
type IGreeterService =
    [<OperationContract>]
    abstract SayHello: request: GreetRequest * context: CallContext -> ValueTask<GreetReply>
```

**Key packages**: `protobuf-net.Grpc.AspNetCore`, `protobuf-net.Grpc.Native`, `protobuf-net-fsharp`

### Contract-First (.proto)

Start with `.proto` files and generate F# code:

- **FsGrpc** — generates immutable F# records and discriminated unions (recommended)
- **Grpc.Tools** — generates C# classes consumed via a project reference

## Important F# + protobuf-net Notes

These were discovered and validated through testing:

- **Registration**: Each F# record type must be registered individually before serialization:
  ```fsharp
  open ProtoBuf.FSharp
  let model = Serialiser.defaultModel
  Serialiser.registerRecordRuntimeTypeIntoModel typeof<MyType> model |> ignore
  ```
- **Collections**: Use `array` (`T[]`) for repeated fields, **not** F# `list<T>`. Use `Dictionary<K,V>` for map fields, **not** F# `Map<K,V>`.
- **Options**: F# `option<T>` works correctly after registration via `protobuf-net-fsharp`.
- **Module name**: `ProtoBuf.FSharp.Serialiser` (British spelling).

## Tests

83 tests covering serialization, type registration, contract validation, service implementations, and end-to-end integration:

```
Passed!  - Failed: 0, Passed: 83, Skipped: 0, Total: 83
```

Run them with:

```bash
cd tests/GrpcSkills.Tests
dotnet test
```

| Category | Tests | What's validated |
|----------|-------|------------------|
| Serialization | 24 | Roundtrip of records, options, arrays, dictionaries, nested types, edge cases |
| Registration | 9 | protobuf-net-fsharp type registration and collection serialization |
| Contract Validation | 25 | Attribute presence, field number uniqueness, return type correctness |
| Service | 15 | Unary, server streaming, bidirectional streaming, error handling |
| Integration | 10 | In-process ASP.NET Core gRPC server + client, concurrent calls |

## Project Structure

```
fsGRPCSkills/
├── grpc-setup/              # Project bootstrapping
│   ├── SKILL.md
│   ├── scripts/setup-grpc.sh
│   └── templates/
├── grpc-proto/              # .proto files and code generation
│   ├── SKILL.md
│   ├── references/proto3-types.md
│   └── templates/
├── grpc-codefirst/          # Code-first contracts
│   ├── SKILL.md
│   ├── references/codefirst-patterns.md
│   └── templates/
├── grpc-server/             # Server implementation
│   ├── SKILL.md
│   ├── references/server-patterns.md
│   └── templates/
├── grpc-client/             # Client implementation
│   ├── SKILL.md
│   ├── references/client-patterns.md
│   └── templates/
└── tests/
    └── GrpcSkills.Tests/    # 83 tests
```

## References

- [grpc-dotnet](https://github.com/grpc/grpc-dotnet) — official .NET gRPC implementation
- [protobuf-net.Grpc](https://github.com/protobuf-net/protobuf-net.Grpc) — code-first gRPC
- [protobuf-net-fsharp](https://github.com/mlivernoche/protobuf-net-fsharp) — F# type support for protobuf-net
- [FsGrpc](https://github.com/mzgkits/FsGrpc) — idiomatic F# code generation from .proto files
