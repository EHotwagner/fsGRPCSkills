---
name: fsgrpc-proto
description: >-
  Use when the user asks to "write a proto file", "create protobuf definitions",
  "generate F# from proto", "set up proto compilation", "define gRPC service
  contract", "convert proto to F#", or needs to work with .proto files and
  generate F# code from them.
version: 0.1.0
---

# Contract-First gRPC with .proto Files

Write .proto files and generate idiomatic F# code from them.

## Overview

Contract-first gRPC starts with `.proto` files that define messages and
services, then generates code. For F#, there are two generation paths:

| Generator | Output | F# Idiom Level | Requirements |
|-----------|--------|-----------------|--------------|
| **FsGrpc** | Immutable F# records, DUs for `oneof` | High | `buf` CLI, FsGrpc NuGet |
| **Grpc.Tools** | C# mutable classes | Low (interop) | Grpc.Tools NuGet |

**Recommendation**: Use FsGrpc for new F# projects. Use Grpc.Tools only when
you must share generated code with C# projects.

## Workflow

### 1. Write the .proto file

Create proto files in a `Protos/` directory. Use proto3 syntax:

```protobuf
syntax = "proto3";

package myapp.v1;

option csharp_namespace = "MyApp.V1";

// Request message
message GreetRequest {
  string name = 1;
}

// Response message
message GreetReply {
  string message = 1;
}

// Service definition
service Greeter {
  // Unary RPC
  rpc SayHello (GreetRequest) returns (GreetReply);

  // Server streaming RPC
  rpc SayHelloStream (GreetRequest) returns (stream GreetReply);

  // Client streaming RPC
  rpc CollectNames (stream GreetRequest) returns (GreetReply);

  // Bidirectional streaming RPC
  rpc Chat (stream GreetRequest) returns (stream GreetReply);
}
```

### 2. Proto file conventions

| Convention | Rule |
|-----------|------|
| File names | `lower_snake_case.proto` |
| Package | `company.project.version` (e.g., `myapp.v1`) |
| Message names | `PascalCase` |
| Field names | `lower_snake_case` |
| Enum values | `UPPER_SNAKE_CASE` with type prefix |
| Service names | `PascalCase` |
| RPC names | `PascalCase` |

### 3. Common proto3 patterns

#### Enums

```protobuf
enum Status {
  STATUS_UNSPECIFIED = 0;  // Always have a zero value
  STATUS_ACTIVE = 1;
  STATUS_INACTIVE = 2;
}
```

#### Nested messages

```protobuf
message Order {
  string id = 1;

  message LineItem {
    string product_id = 1;
    int32 quantity = 2;
    double unit_price = 3;
  }

  repeated LineItem items = 2;
}
```

#### Oneof (maps to F# DU with FsGrpc)

```protobuf
message PaymentMethod {
  oneof method {
    CreditCard credit_card = 1;
    BankTransfer bank_transfer = 2;
    string voucher_code = 3;
  }
}
```

FsGrpc generates:

```fsharp
type PaymentMethod =
    { Method: PaymentMethodCase }

and PaymentMethodCase =
    | CreditCard of CreditCard
    | BankTransfer of BankTransfer
    | VoucherCode of string
```

#### Well-known types

```protobuf
import "google/protobuf/timestamp.proto";
import "google/protobuf/wrappers.proto";
import "google/protobuf/empty.proto";
import "google/protobuf/duration.proto";

message Event {
  google.protobuf.Timestamp created_at = 1;
  google.protobuf.StringValue optional_note = 2;  // nullable
  google.protobuf.Duration timeout = 3;
}
```

#### Maps

```protobuf
message Config {
  map<string, string> settings = 1;
}
```

FsGrpc generates `Map<string, string>` in F#.

### 4. Generate F# code with FsGrpc

#### Set up buf.yaml

```yaml
version: v1
deps:
  - buf.build/googleapis/googleapis
breaking:
  use:
    - FILE
lint:
  use:
    - DEFAULT
```

#### Run generation

FsGrpc integrates with MSBuild. After adding the NuGet packages, simply build:

```bash
dotnet build
```

The `FsGrpc.Tools` package hooks into MSBuild to invoke `buf` and generate
F# source files automatically.

#### Manual generation (if needed)

```bash
buf generate --template buf.gen.yaml Protos/
```

With `buf.gen.yaml`:

```yaml
version: v1
plugins:
  - plugin: fsgrpc
    out: Generated
```

### 5. Generated F# code patterns (FsGrpc)

FsGrpc generates:

| Proto concept | F# output |
|---------------|-----------|
| `message` | Immutable F# record |
| `enum` | F# enum type |
| `oneof` | F# discriminated union |
| `repeated` | `list<T>` |
| `map<K,V>` | `Map<K,V>` |
| `optional` scalar | Value with default |
| `google.protobuf.StringValue` | `string option` |
| `google.protobuf.Timestamp` | `System.DateTimeOffset` |

### 6. Using standard Grpc.Tools (C# interop path)

If you must use the standard toolchain:

1. Create a **C# class library** for the generated code:

```xml
<!-- ProtoLib.csproj (C#) -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Grpc.AspNetCore" Version="2.67.0" />
    <Protobuf Include="..\Protos\*.proto" GrpcServices="Both" />
  </ItemGroup>
</Project>
```

2. Reference from F# project:

```xml
<ProjectReference Include="..\ProtoLib\ProtoLib.csproj" />
```

3. Consume in F#:

```fsharp
open MyApp.V1

let request = GreetRequest(Name = "World")
// Note: these are mutable C# objects, not idiomatic F#
```

## Best Practices

- **DO** use proto3 syntax for all new definitions
- **DO** always include a zero-value `UNSPECIFIED` entry in enums
- **DO** use `oneof` for variant types; FsGrpc maps them to DUs
- **DO** version your packages (e.g., `myapp.v1`, `myapp.v2`)
- **DO** use well-known types (`Timestamp`, wrappers) instead of raw primitives for nullable/temporal fields
- **DO** run `buf lint` to validate proto files
- **DON'T** change field numbers in published protos (breaks wire compatibility)
- **DON'T** remove or rename fields; use `reserved` instead
- **DON'T** use `required` (not available in proto3 anyway)
- **DON'T** put C# generated code directly in an F# project; use a C# intermediary

## Additional Resources

- [Protocol Buffers Language Guide (proto3)](https://protobuf.dev/programming-guides/proto3/)
- [buf CLI documentation](https://buf.build/docs/)
- [FsGrpc documentation](https://github.com/mzgkits/FsGrpc)
- Templates: `templates/greeter.proto`, `templates/buf.yaml`, `templates/buf.gen.yaml`
- Reference: `references/proto3-types.md`

## Implementation Workflow

1. Create `Protos/` directory and `buf.yaml`
2. Write `.proto` file(s) following conventions
3. Run `buf lint` to validate
4. Build project to trigger code generation
5. Verify generated F# code compiles and types are correct
6. Use generated types in server/client code
